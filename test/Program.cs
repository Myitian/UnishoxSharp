using System.Text;
using UnishoxSharp.Common;
using UnishoxV1 = UnishoxSharp.V1.Unishox;
using UnishoxV2 = UnishoxSharp.V2.Unishox;

namespace Test;

class Program
{
    private static int Main()
    {
        Console.InputEncoding = Encoding.UTF8;
        Console.OutputEncoding = Encoding.UTF8;
        if (!Check())
            return 1;
        Console.WriteLine("TEST PASSED!");
        using MemoryStream bk = new();
        using StreamNoSeek ns = new(bk);
        UnishoxLinkList? linkList = null;
        while (Console.ReadLine() is string line)
        {
            byte[] src = Encoding.UTF8.GetBytes(line);
            Console.WriteLine($"src :{BytesToString(src)}");
            Console.WriteLine($"srcs:{src.Length}");

            msTest.SetLength(0);
            int lenV1cC = UnishoxV1.CompressCount(src, linkList);
            int lenV1c = UnishoxV1.Compress(src, msTest, linkList);
            byte[] dataV1c = msTest.ToArray();
            Console.WriteLine($"V1c :{BytesToString(dataV1c)}");
            msTest.SetLength(0);
            int lenV1dC = UnishoxV1.DecompressCount(dataV1c, linkList);
            int lenV1d = UnishoxV1.Decompress(dataV1c, msTest, linkList);
            byte[] dataV1d = msTest.ToArray();
            Console.WriteLine($"V1d :{BytesToString(dataV1d)}");
            Console.WriteLine($"V1x :{Encoding.UTF8.GetString(dataV1d)}");
            bool statusV1 = src.AsSpan().SequenceEqual(dataV1d);
            Console.WriteLine($"V1s :{lenV1cC}/{lenV1c} {lenV1dC}/{lenV1d} {statusV1}");

            msTest.SetLength(0);
            int lenV2FcC = UnishoxV2.CompressCount(src, linkList, true);
            int lenV2Fc = UnishoxV2.Compress(src, msTest, linkList, true);
            byte[] dataV2Fc = msTest.ToArray();
            Console.WriteLine($"V2Fc:{BytesToString(dataV2Fc)}");
            msTest.SetLength(0);
            bk.SetLength(0);
            bk.Write(dataV2Fc);
            bk.Write("123456789"u8); // Extra data test
            bk.Position = 0;
            int lenV2FdC = UnishoxV2.DecompressCount(bk, linkList);
            bk.Position = 0;
            int lenV2Fd = UnishoxV2.Decompress(bk, msTest, linkList);
            byte[] dataV2Fd = msTest.ToArray();
            Console.WriteLine($"V2Fd:{BytesToString(dataV2Fd)}");
            Console.WriteLine($"V2Fx:{Encoding.UTF8.GetString(dataV2Fd)}");
            bool statusV2F = src.AsSpan().SequenceEqual(dataV2Fd);
            Console.WriteLine($"V2Fs:{lenV2FcC}/{lenV2Fc} {lenV2FdC}/{lenV2Fd} {statusV2F}");

            msTest.SetLength(0);
            int lenV2NcC = UnishoxV2.CompressCount(src, linkList);
            int lenV2Nc = UnishoxV2.Compress(src, msTest, linkList);
            byte[] dataV2Nc = msTest.ToArray();
            Console.WriteLine($"V2Nc:{BytesToString(dataV2Nc)}");
            msTest.SetLength(0);
            int lenV2NdC = UnishoxV2.DecompressCount(dataV2Fc, linkList);
            int lenV2Nd = UnishoxV2.Decompress(dataV2Fc, msTest, linkList);
            byte[] dataV2Nd = msTest.ToArray();
            Console.WriteLine($"V2Nd:{BytesToString(dataV2Nd)}");
            Console.WriteLine($"V2Nx:{Encoding.UTF8.GetString(dataV2Nd)}");
            bool statusV2N = src.AsSpan().SequenceEqual(dataV2Nd);
            Console.WriteLine($"V2Ns:{lenV2NcC}/{lenV2Nc} {lenV2NdC}/{lenV2Nd} {statusV2N}");
            linkList = new()
            {
                Data = src,
                Previous = linkList
            };
        }
        return 0;
    }

    static readonly MemoryStream msTest = new();
    static readonly MemoryStream msT2 = new();
    static bool DoTest(scoped ReadOnlySpan<byte> chars)
    {
        msTest.SetLength(0);
        int len = UnishoxV2.Compress(chars, msTest, null, true);
        byte[] data = msTest.ToArray();
        msT2.SetLength(0);
        msTest.Position = 0;
        msTest.CopyTo(msT2);
        msTest.SetLength(0);
        msT2.Write("abcdefg"u8); // Extra data test
        msT2.Position = 0;
        int len2 = UnishoxV2.Decompress(msT2, msTest, null);
        byte[] data2 = msTest.ToArray();
        msTest.SetLength(0);
        int len3 = UnishoxV2.Decompress(msT2.ToArray(), msTest, null);
        byte[] data3 = msTest.ToArray();

        msTest.SetLength(0);
        bool status = chars.SequenceEqual(data2);
        if (!status)
        {
            Console.WriteLine("V2 FAILED!");
            Console.WriteLine(BytesToString(chars));
            Console.WriteLine(BytesToString(data));
            Console.WriteLine(BytesToString(data.AsSpan(0, len)));
            Console.WriteLine(BytesToString(data2));
            Console.WriteLine(BytesToString(data2.AsSpan(0, len2)));
            Console.WriteLine(BytesToString(data3));
            Console.WriteLine(BytesToString(data3.AsSpan(0, len3)));
        }
        return status && DoTestV1(chars);
    }
    static bool DoTestV1(scoped ReadOnlySpan<byte> chars)
    {
        msTest.SetLength(0);
        int len = UnishoxV1.Compress(chars, msTest, null, true);
        byte[] data = msTest.ToArray();
        msT2.SetLength(0);
        msTest.Position = 0;
        msTest.CopyTo(msT2);
        msTest.SetLength(0);
        msT2.Position = 0;
        int len2 = UnishoxV1.Decompress(msT2, msTest, null);
        byte[] data2 = msTest.ToArray();
        msTest.SetLength(0);
        int len3 = UnishoxV1.Decompress(msT2.ToArray(), msTest, null);
        byte[] data3 = msTest.ToArray();

        msTest.SetLength(0);
        bool status = chars.SequenceEqual(data2);
        if (!status)
        {
            Console.WriteLine("V1 FAILED!");
            Console.WriteLine(BytesToString(chars));
            Console.WriteLine(BytesToString(data));
            Console.WriteLine(BytesToString(data.AsSpan(0, len)));
            Console.WriteLine(BytesToString(data2));
            Console.WriteLine(BytesToString(data2.AsSpan(0, len2)));
            Console.WriteLine(BytesToString(data3));
            Console.WriteLine(BytesToString(data3.AsSpan(0, len3)));
        }
        return status;
    }
    static StringBuilder BytesToString(scoped ReadOnlySpan<byte> bytes, int limit = int.MaxValue, StringBuilder? sb = null)
    {
        sb ??= new();
        sb.Append("b\"");
        for (int i = 0; i < bytes.Length; i++)
        {
            if (i == limit)
                return sb.Append($"\" ... and {bytes.Length - limit} more");
            byte b = bytes[i];
            switch (b)
            {
                case (byte)'\t':
                    sb.Append("\\t");
                    break;
                case (byte)'\n':
                    sb.Append("\\n");
                    break;
                case (byte)'\r':
                    sb.Append("\\r");
                    break;
                case (byte)'\"':
                    sb.Append("\\\"");
                    break;
                case (byte)'\\':
                    sb.Append("\\\\");
                    break;
                default:
                    if (0x1F < b && b < 0x7F)
                        sb.Append((char)b);
                    else
                        sb.Append($"\\x{b:x2}");
                    break;
            }
        }
        return sb.Append('"');
    }
    static bool Check()
    {
        // Basic
        if (!DoTest("Hello"u8)) return false;
        if (!DoTest("Hello World"u8)) return false;
        if (!DoTest("The quick brown fox jumped over the lazy dog"u8)) return false;
        if (!DoTest("HELLO WORLD"u8)) return false;
        if (!DoTest("HELLO WORLD HELLO WORLD"u8)) return false;

        // Numbers
        if (!DoTest("Hello1"u8)) return false;
        if (!DoTest("Hello1 World2"u8)) return false;
        if (!DoTest("Hello123"u8)) return false;
        if (!DoTest("12345678"u8)) return false;
        if (!DoTest("12345678 12345678"u8)) return false;
        if (!DoTest("HELLO WORLD 1234 hello world12"u8)) return false;
        if (!DoTest("HELLO 234 WORLD"u8)) return false;
        if (!DoTest("9 HELLO, WORLD"u8)) return false;
        if (!DoTest("H1e2l3l4o5 w6O7R8L9D"u8)) return false;
        if (!DoTest("8+80=88"u8)) return false;

        // Symbols
        if (!DoTest("~!@#$%^&*()_+=-`;'\\|\":,./?><"u8)) return false;
        if (!DoTest("if (!test_ushx_cd(\"H1e2l3l4o5 w6O7R8L9D\")) return 1;"u8)) return false;

        // Repeat
        if (!DoTest("-----------------///////////////"u8)) return false;
        if (!DoTest("-----------------Hello World1111111111112222222abcdef12345abcde1234_////////Hello World///////"u8)) return false;

        if (!DoTest("Cada buhonero alaba sus agujas. - A peddler praises his needles (wares)."u8)) return false;
        if (!DoTest("Cada gallo canta en su muladar. - Each rooster sings on its dung-heap."u8)) return false;
        if (!DoTest("Cada martes tiene su domingo. - Each Tuesday has its Sunday."u8)) return false;
        if (!DoTest("Cada uno habla de la feria como le va en ella. - Our way of talking about things reflects our relevant experience, good or bad."u8)) return false;
        if (!DoTest("Dime con quien andas y te diré quién eres.. - Tell me who you walk with, and I will tell you who you are."u8)) return false;
        if (!DoTest("Donde comen dos, comen tres. - You can add one person more in any situation you are managing."u8)) return false;
        if (!DoTest("El amor es ciego. - Love is blind"u8)) return false;
        if (!DoTest("El amor todo lo iguala. - Love smoothes life out."u8)) return false;
        if (!DoTest("El tiempo todo lo cura. - Time cures all."u8)) return false;
        if (!DoTest("La avaricia rompe el saco. - Greed bursts the sack."u8)) return false;
        if (!DoTest("La cara es el espejo del alma. - The face is the mirror of the soul."u8)) return false;
        if (!DoTest("La diligencia es la madre de la buena ventura. - Diligence is the mother of good fortune."u8)) return false;
        if (!DoTest("La fe mueve montañas. - Faith moves mountains."u8)) return false;
        if (!DoTest("La mejor palabra siempre es la que queda por decir. - The best word is the one left unsaid."u8)) return false;
        if (!DoTest("La peor gallina es la que más cacarea. - The worst hen is the one that clucks the most."u8)) return false;
        if (!DoTest("La sangre sin fuego hierve. - Blood boils without fire."u8)) return false;
        if (!DoTest("La vida no es un camino de rosas. - Life is not a path of roses."u8)) return false;
        if (!DoTest("Las burlas se vuelven veras. - Bad jokes become reality."u8)) return false;
        if (!DoTest("Las desgracias nunca vienen solas. - Misfortunes never come one at a time."u8)) return false;
        if (!DoTest("Lo comido es lo seguro. - You can only be really certain of what is already in your belly."u8)) return false;
        if (!DoTest("Los años no pasan en balde. - Years don't pass in vain."u8)) return false;
        if (!DoTest("Los celos son malos consejeros. - Jealousy is a bad counsellor."u8)) return false;
        if (!DoTest("Los tiempos cambian. - Times change."u8)) return false;
        if (!DoTest("Mañana será otro día. - Tomorrow will be another day."u8)) return false;
        if (!DoTest("Ningún jorobado ve su joroba. - No hunchback sees his own hump."u8)) return false;
        if (!DoTest("No cantan dos gallos en un gallinero. - Two roosters do not crow in a henhouse."u8)) return false;
        if (!DoTest("No hay harina sin salvado. - No flour without bran."u8)) return false;
        if (!DoTest("No por mucho madrugar, amanece más temprano.. - No matter if you rise early because it does not sunrise earlier."u8)) return false;
        if (!DoTest("No se puede hacer tortilla sin romper los huevos. - One can't make an omelette without breaking eggs."u8)) return false;
        if (!DoTest("No todas las verdades son para dichas. - Not every truth should be said."u8)) return false;
        if (!DoTest("No todo el monte es orégano. - The whole hillside is not covered in spice."u8)) return false;
        if (!DoTest("Nunca llueve a gusto de todos. - It never rains to everyone's taste."u8)) return false;
        if (!DoTest("Perro ladrador, poco mordedor.. - A dog that barks often seldom bites."u8)) return false;
        if (!DoTest("Todos los caminos llevan a Roma. - All roads lead to Rome."u8)) return false;

        // Unicode
        if (!DoTest("案ずるより産むが易し。 - Giving birth to a baby is easier than worrying about it."u8)) return false;
        if (!DoTest("出る杭は打たれる。 - The stake that sticks up gets hammered down."u8)) return false;
        if (!DoTest("知らぬが仏。 - Not knowing is Buddha. - Ignorance is bliss."u8)) return false;
        if (!DoTest("見ぬが花。 - Not seeing is a flower. - Reality can't compete with imagination."u8)) return false;
        if (!DoTest("花は桜木人は武士 - Of flowers, the cherry blossom; of men, the warrior."u8)) return false;

        if (!DoTest("小洞不补，大洞吃苦 - A small hole not mended in time will become a big hole much more difficult to mend."u8)) return false;
        if (!DoTest("读万卷书不如行万里路 - Reading thousands of books is not as good as traveling thousands of miles"u8)) return false;
        if (!DoTest("福无重至,祸不单行 - Fortune does not come twice. Misfortune does not come alone."u8)) return false;
        if (!DoTest("风向转变时,有人筑墙,有人造风车 - When the wind changes, some people build walls and have artificial windmills."u8)) return false;
        if (!DoTest("父债子还 - Father's debt, son to give back."u8)) return false;
        if (!DoTest("害人之心不可有 - Do not harbour intentions to hurt others."u8)) return false;
        if (!DoTest("今日事，今日毕 - Things of today, accomplished today."u8)) return false;
        if (!DoTest("空穴来风,未必无因 - Where there's smoke, there's fire."u8)) return false;
        if (!DoTest("良药苦口 - Good medicine tastes bitter."u8)) return false;
        if (!DoTest("人算不如天算 - Man proposes and God disposes"u8)) return false;
        if (!DoTest("师傅领进门，修行在个人 - Teachers open the door. You enter by yourself."u8)) return false;
        if (!DoTest("授人以鱼不如授之以渔 - Teach a man to take a fish is not equal to teach a man how to fish."u8)) return false;
        if (!DoTest("树倒猢狲散 - When the tree falls, the monkeys scatter."u8)) return false;
        if (!DoTest("水能载舟，亦能覆舟 - Not only can water float a boat, it can sink it also."u8)) return false;
        if (!DoTest("朝被蛇咬，十年怕井绳 - Once bitten by a snake for a snap dreads a rope for a decade."u8)) return false;
        if (!DoTest("一分耕耘，一分收获 - If one does not plow, there will be no harvest."u8)) return false;
        if (!DoTest("有钱能使鬼推磨 - If you have money you can make the devil push your grind stone."u8)) return false;
        if (!DoTest("一失足成千古恨，再回头已百年身 - A single slip may cause lasting sorrow."u8)) return false;
        if (!DoTest("自助者天助 - Those who help themselves, God will help."u8)) return false;
        if (!DoTest("早起的鸟儿有虫吃 - Early bird gets the worm."u8)) return false;
        if (!DoTest("{\"menu\": {\n  \"id\": \"file\",\n  \"value\": \"File\",\n  \"popup\": {\n    \"menuitem\": [\n      {\"value\": \"New\", \"onclick\": \"CreateNewDoc()\"},\n      {\"value\": \"Open\", \"onclick\": \"OpenDoc()\"},\n      {\"value\": \"Close\", \"onclick\": \"CloseDoc()\"}\n    ]\n  }\n}}"u8)) return false;

        // English
        if (!DoTest("Beauty is not in the face. Beauty is a light in the heart."u8)) return false;
        // Spanish
        if (!DoTest("La belleza no está en la cara. La belleza es una luz en el corazón."u8)) return false;
        // French
        if (!DoTest("La beauté est pas dans le visage. La beauté est la lumière dans le coeur."u8)) return false;
        // Portugese
        if (!DoTest("A beleza não está na cara. A beleza é a luz no coração."u8)) return false;
        // Dutch
        if (!DoTest("Schoonheid is niet in het gezicht. Schoonheid is een licht in het hart."u8)) return false;

        // German
        if (!DoTest("Schönheit ist nicht im Gesicht. Schönheit ist ein Licht im Herzen."u8)) return false;
        // Spanish
        if (!DoTest("La belleza no está en la cara. La belleza es una luz en el corazón."u8)) return false;
        // French
        if (!DoTest("La beauté est pas dans le visage. La beauté est la lumière dans le coeur."u8)) return false;
        // Italian
        if (!DoTest("La bellezza non è in faccia. La bellezza è la luce nel cuore."u8)) return false;
        // Swedish
        if (!DoTest("Skönhet är inte i ansiktet. Skönhet är ett ljus i hjärtat."u8)) return false;
        // Romanian
        if (!DoTest("Frumusețea nu este în față. Frumusețea este o lumină în inimă."u8)) return false;
        // Ukranian
        if (!DoTest("Краса не в особі. Краса - це світло в серці."u8)) return false;
        // Greek
        if (!DoTest("Η ομορφιά δεν είναι στο πρόσωπο. Η ομορφιά είναι ένα φως στην καρδιά."u8)) return false;
        // Turkish
        if (!DoTest("Güzellik yüzünde değil. Güzellik, kalbin içindeki bir ışıktır."u8)) return false;
        // Polish
        if (!DoTest("Piękno nie jest na twarzy. Piękno jest światłem w sercu."u8)) return false;

        // Africans
        if (!DoTest("Skoonheid is nie in die gesig nie. Skoonheid is 'n lig in die hart."u8)) return false;
        // Swahili
        if (!DoTest("Beauty si katika uso. Uzuri ni nuru moyoni."u8)) return false;
        // Zulu
        if (!DoTest("Ubuhle abukho ebusweni. Ubuhle bungukukhanya enhliziyweni."u8)) return false;
        // Somali
        if (!DoTest("Beauty ma aha in wajiga. Beauty waa iftiin ah ee wadnaha."u8)) return false;

        // Russian
        if (!DoTest("Красота не в лицо. Красота - это свет в сердце."u8)) return false;
        // Arabic
        if (!DoTest("الجمال ليس في الوجه. الجمال هو النور الذي في القلب."u8)) return false;
        // Persian
        if (!DoTest("زیبایی در چهره نیست. زیبایی نور در قلب است."u8)) return false;
        // Pashto
        if (!DoTest("ښکلا په مخ کې نه ده. ښکلا په زړه کی یوه رڼا ده."u8)) return false;
        // Azerbaijani
        if (!DoTest("Gözəllik üzdə deyil. Gözəllik qəlbdə bir işıqdır."u8)) return false;
        // Uzbek
        if (!DoTest("Go'zallik yuzida emas. Go'zallik - qalbdagi nur."u8)) return false;
        // Kurdish
        if (!DoTest("Bedewî ne di rû de ye. Bedewî di dil de ronahiyek e."u8)) return false;
        // Urdu
        if (!DoTest("خوبصورتی چہرے میں نہیں ہے۔ خوبصورتی دل میں روشنی ہے۔"u8)) return false;

        // Hindi
        if (!DoTest("सुंदरता चेहरे में नहीं है। सौंदर्य हृदय में प्रकाश है।"u8)) return false;
        // Bangla
        if (!DoTest("সৌন্দর্য মুখে নেই। সৌন্দর্য হৃদয় একটি আলো।"u8)) return false;
        // Punjabi
        if (!DoTest("ਸੁੰਦਰਤਾ ਚਿਹਰੇ ਵਿੱਚ ਨਹੀਂ ਹੈ. ਸੁੰਦਰਤਾ ਦੇ ਦਿਲ ਵਿਚ ਚਾਨਣ ਹੈ."u8)) return false;
        // Telugu
        if (!DoTest("అందం ముఖంలో లేదు. అందం హృదయంలో ఒక కాంతి."u8)) return false;
        // Tamil
        if (!DoTest("அழகு முகத்தில் இல்லை. அழகு என்பது இதயத்தின் ஒளி."u8)) return false;
        // Marathi
        if (!DoTest("सौंदर्य चेहरा नाही. सौंदर्य हे हृदयातील एक प्रकाश आहे."u8)) return false;
        // Kannada
        if (!DoTest("ಸೌಂದರ್ಯವು ಮುಖದ ಮೇಲೆ ಇಲ್ಲ. ಸೌಂದರ್ಯವು ಹೃದಯದಲ್ಲಿ ಒಂದು ಬೆಳಕು."u8)) return false;
        // Gujarati
        if (!DoTest("સુંદરતા ચહેરા પર નથી. સુંદરતા હૃદયમાં પ્રકાશ છે."u8)) return false;
        // Malayalam
        if (!DoTest("സൗന്ദര്യം മുഖത്ത് ഇല്ല. സൗന്ദര്യം ഹൃദയത്തിലെ ഒരു പ്രകാശമാണ്."u8)) return false;
        // Nepali
        if (!DoTest("सौन्दर्य अनुहारमा छैन। सौन्दर्य मुटुको उज्यालो हो।"u8)) return false;
        // Sinhala
        if (!DoTest("රූපලාවන්ය මුහුණේ නොවේ. රූපලාවන්ය හදවත තුළ ඇති ආලෝකය වේ."u8)) return false;

        // Chinese
        if (!DoTest("美是不是在脸上。 美是心中的亮光。"u8)) return false;
        // Javanese
        if (!DoTest("Beauty ora ing pasuryan. Kaendahan iku cahya ing sajroning ati."u8)) return false;
        // Japanese
        if (!DoTest("美は顔にありません。美は心の中の光です。"u8)) return false;
        // Filipino
        if (!DoTest("Ang kagandahan ay wala sa mukha. Ang kagandahan ay ang ilaw sa puso."u8)) return false;
        // Korean
        if (!DoTest("아름다움은 얼굴에 없습니다。아름다움은 마음의 빛입니다。"u8)) return false;
        // Vietnam
        if (!DoTest("Vẻ đẹp không nằm trong khuôn mặt. Vẻ đẹp là ánh sáng trong tim."u8)) return false;
        // Thai
        if (!DoTest("ความงามไม่ได้อยู่ที่ใบหน้า ความงามเป็นแสงสว่างในใจ"u8)) return false;
        // Burmese
        if (!DoTest("အလှအပမျက်နှာပေါ်မှာမဟုတ်ပါဘူး။ အလှအပစိတ်နှလုံးထဲမှာအလင်းကိုဖြစ်ပါတယ်။"u8)) return false;
        // Malay
        if (!DoTest("Kecantikan bukan di muka. Kecantikan adalah cahaya di dalam hati."u8)) return false;

        // Emoji
        if (!DoTest("🤣🤣🤣🤣🤣🤣🤣🤣🤣🤣🤣"u8)) return false;
        return true;
    }
}

class StreamNoSeek(Stream stream) : Stream
{
    public override bool CanRead => stream.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => stream.CanWrite;
    public override long Length => throw new Exception();
    public override long Position { get => stream.Position; set => throw new Exception(); }
    public override void Flush()
    {
        stream.Flush();
    }
    public override int Read(byte[] buffer, int offset, int count)
    {
        return stream.Read(buffer, offset, count);
    }
    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new Exception();
    }
    public override void SetLength(long value)
    {
        throw new Exception();
    }
    public override void Write(byte[] buffer, int offset, int count)
    {
        stream.Write(buffer, offset, count);
    }
}