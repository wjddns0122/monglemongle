using System.Linq;
using Mongle.Data;

namespace Mongle.Village
{
    /// <summary>A "?" question a friend asks about the player (D-011). Choices are said as they are ("딸기 케이크를 좋아하는구나").</summary>
    public class FriendQuestion
    {
        public string key, label, prompt;
        public string[] choices, comments;   // a little reaction per choice (same order)
        public string Comment(string choice) { int i = System.Array.IndexOf(choices, choice); return i >= 0 && comments != null && i < comments.Length ? comments[i] : ""; }
    }

    public static class FriendQuestions
    {
        public static readonly FriendQuestion[] All =
        {
            new FriendQuestion { key = PlayerProfile.Birthday, label = "생일", prompt = "너는 생일이 언제야? 그날 꼭 축하해 주고 싶어!" },
            new FriendQuestion { key = "Food", label = "좋아하는 음식", prompt = "너는 무슨 음식을 제일 좋아해?",
                choices = new[] { "딸기 케이크", "쿠키", "푸딩", "사탕", "과일", "아이스크림" },
                comments = new[] { "새콤달콤 딸기가 콕 올라간 거!", "바삭바삭 고소하지!", "말랑말랑 탱글탱글!", "사탕 나무 열매도 달콤해!", "상큼한 게 최고지!", "차갑고 달콤해!" } },
            new FriendQuestion { key = "Toy", label = "좋아하는 장난감", prompt = "너는 어떤 장난감을 제일 좋아해?",
                choices = new[] { "인형", "로봇", "자동차", "블록", "공룡", "비행기" },
                comments = new[] { "포근하게 안아 주고 싶지!", "삐삐가 들으면 삐빅 춤출 거야!", "부릉부릉 신나게 달리자!", "높이높이 쌓아 보자!", "크앙! 멋지잖아!", "슝 하고 하늘을 나는 상상을 해 봐!" } },
            new FriendQuestion { key = "Color", label = "좋아하는 색", prompt = "너는 무슨 색을 제일 좋아해?",
                choices = new[] { "분홍색", "하늘색", "노란색", "민트색", "보라색", "빨간색" },
                comments = new[] { "솜사탕 같은 색이야!", "맑은 하늘 같아!", "햇살 같은 색!", "시원하고 산뜻해!", "꿈나라 같은 색이야!", "딸기처럼 빨갛다!" } },
            new FriendQuestion { key = "Season", label = "좋아하는 계절", prompt = "너는 어느 계절이 제일 좋아?",
                choices = new[] { "봄", "여름", "가을", "겨울" },
                comments = new[] { "꽃이 활짝 피잖아!", "물놀이 하기 딱 좋아!", "낙엽 밟는 소리가 좋아!", "눈사람 만들자!" } },
            new FriendQuestion { key = "Animal", label = "좋아하는 동물", prompt = "너는 어떤 동물을 제일 좋아해?",
                choices = new[] { "토끼", "고양이", "곰", "강아지", "병아리", "펭귄", "개구리" },
                comments = new[] { "깡총깡총 귀엽지!", "살금살금 낮잠 자는 게 귀여워!", "몽이도 곰이잖아!", "꼬리 흔드는 게 귀여워!", "삐약삐약 노랗고 작아!", "뒤뚱뒤뚱 걷는 게 귀여워!", "개굴개굴 노래를 잘하지!" } },
            new FriendQuestion { key = "Ride", label = "좋아하는 놀이기구", prompt = "공원 놀이기구 중에 뭐가 제일 좋아?",
                choices = new[] { "회전목마", "관람차", "열기구" },
                comments = new[] { "빙글빙글 말을 타는 거!", "높이 올라가면 마을이 다 보여!", "둥실둥실 하늘을 나는 거!" } },
        };

        public static string[] Keys => All.Select(q => q.key).ToArray();
        public static FriendQuestion Get(string key) => All.FirstOrDefault(q => q.key == key);

        /// <summary>What animal a friend is, as the Animal question says it (for "나 같은 개구리?!"); null for robots, cars and the like.</summary>
        public static string AnimalWordOf(string kindId)
        {
            if (string.IsNullOrEmpty(kindId)) return null;
            if (kindId.StartsWith("Bunny")) return "토끼";
            if (kindId.StartsWith("Cat")) return "고양이";
            if (kindId.StartsWith("TeddyBear")) return "곰";
            if (kindId.StartsWith("Frog")) return "개구리";
            return null;
        }

        /// <summary>What kind of toy a friend is, as the Toy question says it (for "나 같은 로봇?!").</summary>
        public static string ToyWordOf(string kindId)
        {
            if (string.IsNullOrEmpty(kindId)) return null;
            if (kindId.StartsWith("Robot")) return "로봇";
            if (kindId.StartsWith("Car") || kindId.StartsWith("FireTruck")) return "자동차";
            if (kindId.StartsWith("Dino")) return "공룡";
            if (kindId.StartsWith("Plane")) return "비행기";
            return "인형";
        }
    }

    /// <summary>The friends' lines for requests and questions (D-011), a little different per personality. Warm 반말.</summary>
    public static class FriendQuestText
    {
        static int P(Personality p) => (int)p; // Shy, Energetic, Sleepy, Curious, Gentle, Mischievous

        public static string Title(FriendQuestKind k)
        {
            switch (k)
            {
                case FriendQuestKind.Fruit: return "열매 따기";
                case FriendQuestKind.Stars: return "별 찾기";
                case FriendQuestKind.Ride: return "같이 타기";
                case FriendQuestKind.Dress: return "꾸며주기";
                default: return "꽃 물주기";
            }
        }

        /// <summary>The short "what to do" line for the quest window.</summary>
        public static string Detail(FriendQuestKind k, int target, string ride, string giverName)
        {
            switch (k)
            {
                case FriendQuestKind.Fruit: return "사탕 나무를 눌러 흔들고, 떨어진 열매 " + target + "개 줍기";
                case FriendQuestKind.Stars: return "공원에 숨은 반짝 별 " + FriendQuestBook.StarCount + "개를 시간 안에 모두 찾기";
                case FriendQuestKind.Ride: return Josa.EulReul(ride) + " 누르면 " + Josa.IGa(giverName) + " 타러 가요. 같이 타요!";
                case FriendQuestKind.Dress: return Josa.EulReul(giverName) + " 누르고 '꾸며주기'에서 악세서리 입혀 주기";
                default: return "화분을 눌러서 물 " + target + "번 주기";
            }
        }

        static readonly string[] Hey = { "저기…", "있잖아!", "으음…", "궁금한 게 있어!", "잠깐 괜찮아?", "헤헤, 있잖아!" };
        /// <summary>The first word when the player comes over (shown in the toy's speech bubble).</summary>
        public static string Greeting(Personality p, OfferType t) => t == OfferType.Question && p != Personality.Shy && p != Personality.Sleepy ? "물어볼 게 있어!" : Hey[P(p)];

        static readonly string[][] Requests =
        {
            // Fruit {0} = count
            new[] { "저기… 사탕 나무 열매가 먹고 싶은데, {0}개만 따다 줄 수 있어?", "있잖아 있잖아! 나무를 흔들어서 열매 {0}개 따다 줄래? 같이 먹자!", "으음… 배고파… 나무를 흔들어서 열매 {0}개만 따다 줄래…?",
                    "사탕 나무를 흔들면 열매가 떨어진대! 열매 {0}개 따다 줄래?", "친구들이랑 나눠 먹고 싶어. 나무를 흔들어서 열매 {0}개 따다 줄래?", "헤헤, 나무를 마구 흔들어 보자! 열매 {0}개 따다 줄래?" },
            // Stars {0} = count
            new[] { "밤에 떨어진 별 조각들이 공원에 숨어 있대… 같이 찾아 줄래?", "공원에 반짝 별 {0}개가 숨었어! 시간 안에 다 찾아 볼래?", "꿈에서 별이 떨어졌어… 공원 어딘가에 {0}개… 찾아 줄래…?",
                    "반짝반짝 별이 공원 여기저기 떨어졌대! 같이 {0}개 다 찾아보자!", "별님들이 길을 잃었나 봐. 공원에 있는 별 {0}개를 찾아 줄래?", "내가 별 {0}개를 공원 곳곳에 숨겨 놨지롱! 시간 안에 찾을 수 있을까?" },
            // Ride {1} = ride
            new[] { "{1} 혼자 타기는 좀 무서워… 같이 타 줄래?", "{1} 타러 가자! 같이 타면 두 배로 신나!", "{1} 타면서 꾸벅꾸벅 졸고 싶어… 같이 타 줄래?",
                    "{1} 위에서 보면 마을이 어떻게 보일까? 같이 타 보자!", "오늘 날씨가 좋아서 {1} 타고 싶어. 같이 탈래?", "{1} 누가 더 신나게 타나 내기하자! 같이 타 줄 거지?" },
            // Dress
            new[] { "나도… 예쁜 악세서리 하나 해 보고 싶어. 골라 줄래?", "나 멋쟁이가 되고 싶어! 악세서리 하나 입혀 줄래?", "낮잠 자기 전에… 예쁘게 꾸며 줄래…?",
                    "악세서리를 하면 내가 어떻게 보일까? 하나 입혀 줘!", "오늘은 특별한 날 같아. 나를 예쁘게 꾸며 줄래?", "헤헤, 깜짝 변신하고 싶어! 악세서리 하나 입혀 줘!" },
            // Water {0} = count
            new[] { "꽃들이 목말라 보여… 화분에 물을 {0}번 줄 수 있어?", "꽃들한테 물 주기 대작전! 화분에 물을 {0}번 줘 볼래?", "꽃도 나처럼 목이 마를 거야… 물 {0}번만 줄래…?",
                    "물을 주면 꽃이 얼마나 자랄까? 화분에 물을 {0}번 줘 보자!", "꽃들이 활짝 웃었으면 좋겠어. 화분에 물을 {0}번 줄래?", "꽃한테 물 뿌리기 놀이 하자! 화분에 물을 {0}번 줘 봐!" },
        };

        public static string Request(FriendQuestKind k, Personality p, int target, string ride, string giverKind = null)
        {
            int n = k == FriendQuestKind.Stars ? FriendQuestBook.StarCount : target;
            if (giverKind != null && giverKind.StartsWith("Frog"))
            {
                // 개굴이 loves the pond, the rain and lily pads
                if (k == FriendQuestKind.Water) return "꽃들도 비 맞은 연잎처럼 촉촉했으면 좋겠어. 화분에 물을 " + n + "번 줄래? 개굴!";
                if (k == FriendQuestKind.Fruit) return "연잎 위에 열매를 올려 두고 소풍하고 싶어. 나무를 흔들어서 열매 " + n + "개 따다 줄래? 개굴!";
            }
            return string.Format(Requests[(int)k][P(p)], n, ride ?? "");
        }

        public static string AcceptLine(FriendQuestKind k, string ride)
        {
            switch (k)
            {
                case FriendQuestKind.Fruit: return "고마워! 사탕 나무를 누르면 흔들 수 있어!";
                case FriendQuestKind.Stars: return "좋아! 반짝이는 별을 눌러서 다 모아 줘!";
                case FriendQuestKind.Ride: return "야호! " + Josa.EulReul(ride) + " 누르면 내가 타러 갈게!";
                case FriendQuestKind.Dress: return "신난다! 두근두근 기다릴게!";
                default: return "고마워! 화분을 누르면 물을 줄 수 있어!";
            }
        }

        static readonly string[] Thanks = { "고, 고마워… 정말 기뻐!", "최고야! 너랑 하니까 엄청 재밌어!", "고마워… 오늘 좋은 꿈 꿀 것 같아…", "우와, 해냈다! 다음엔 또 뭐 하지?", "정말 고마워. 마음이 몽글몽글해졌어.", "헤헤, 역시 너밖에 없어!" };

        public static string Done(FriendQuestKind k, Personality p)
        {
            string head;
            switch (k)
            {
                case FriendQuestKind.Fruit: head = "냠냠! "; break;
                case FriendQuestKind.Stars: head = "별을 다 찾았다! "; break;
                case FriendQuestKind.Ride: head = "같이 타서 너무 신났어! "; break;
                case FriendQuestKind.Dress: head = "나 어때? "; break;
                default: head = "꽃들이 좋아해! "; break;
            }
            return head + Thanks[P(p)];
        }

        static readonly string[] Later = { "으, 응… 기다릴게!", "알았어! 나중에 꼭 와야 해!", "응… 그럼 조금 자고 있을게…", "알았어! 그때 또 물어볼게!", "괜찮아, 천천히 와도 돼.", "헤헤, 나중에 꼭이다!" };
        public static string LaterLine(Personality p) => Later[P(p)];

        /// <summary>Clicking a friend who is waiting on the player: a small reminder.</summary>
        public static string Remind(FriendQuest q, string ride)
        {
            switch (q.kind)
            {
                case FriendQuestKind.Fruit: return "열매 " + q.Left + "개만 더 부탁해!";
                case FriendQuestKind.Stars: return "반짝 별 찾기, 준비되면 '퀘스트'에서 시작해 줘!";
                case FriendQuestKind.Ride: return ride + " 같이 타기로 한 거 기억하지?";
                case FriendQuestKind.Dress: return "나 꾸며 주기로 했지? 두근두근!";
                default: return "꽃에 물 " + q.Left + "번만 더!";
            }
        }

        // ---------------- questions ----------------
        public static string Ask(FriendQuestion q, Personality p)
        {
            string lead = p == Personality.Shy ? "저기… " : p == Personality.Sleepy ? "으음… " : p == Personality.Mischievous ? "헤헤, " : "";
            return lead + q.prompt;
        }

        /// <summary>The friend's warm answer to what the player chose (a birthday arrives as "month-day").</summary>
        public static string Reaction(FriendQuestion q, string value, string askerKind, System.DateTime today)
        {
            if (q.key == PlayerProfile.Birthday)
            {
                if (!PlayerProfile.ParseBirthday(value, out int m, out int d)) return "꼭 기억할게!";
                if (PlayerProfile.IsBirthday(m, d, today)) return "어? 오늘이잖아! 생일 축하해!!";
                return m + "월 " + d + "일! 꼭 기억할게. 그날 다 같이 축하하자!";
            }
            if (q.key == "Toy" && value == FriendQuestions.ToyWordOf(askerKind)) return "나 같은 " + value + "?! 우와, 정말 기뻐!";
            if (q.key == "Animal" && value == FriendQuestions.AnimalWordOf(askerKind)) return "나 같은 " + value + "?! " + (askerKind.StartsWith("Frog") ? "개굴개굴, " : "") + "정말 기뻐!";
            var own = KindReaction(askerKind, q.key, value); if (own != null) return own;
            string tail = q.Comment(value);
            return Josa.EulReul(value) + " 좋아하는구나! " + tail;
        }

        /// <summary>A line that remembers something the player said, or null when nothing fits.</summary>
        public static string Remember(PlayerProfile profile, System.Random rng, System.DateTime today)
        {
            var keys = profile.Keys.ToList();
            if (keys.Count == 0) return null;
            var key = keys[rng.Next(keys.Count)];
            var v = profile.Get(key);
            switch (key)
            {
                case PlayerProfile.Birthday:
                    if (!PlayerProfile.ParseBirthday(v, out int m, out int d)) return null;
                    return m + "월 " + d + "일이 생일이라고 했지? 달력에 동그라미 쳐 놨어!";
                case "Food": return Josa.EulReul(v) + " 좋아한다고 했지? 나도 한 입 먹어 보고 싶어!";
                case "Toy": return Josa.EulReul(v) + " 좋아한다고 했지? 나랑도 같이 놀자!";
                case "Color": return Josa.EulReul(v) + " 좋아한다고 했지? 오늘 공원에도 " + Josa.IGa(v) + " 보여!";
                case "Season": return Josa.EulReul(v) + " 좋아한다고 했지? 빨리 왔으면 좋겠다!";
                case "Animal": return Josa.EulReul(v) + " 좋아한다고 했지? 나도 정말 귀엽다고 생각해!";
                case "Ride": return Josa.EulReul(v) + " 제일 좋아한다고 했지? 나중에 같이 타러 가자!";
            }
            return null;
        }

        /// <summary>A friend's own reaction to an answer that touches what it loves (개굴이: the pond, rain, lily pads).</summary>
        static string KindReaction(string kind, string key, string value)
        {
            if (string.IsNullOrEmpty(kind)) return null;
            if (kind.StartsWith("Frog"))
            {
                if (key == "Season" && value == "여름") return "여름을 좋아하는구나! 장마철 빗소리, 나도 제일 좋아해!";
                if (key == "Color" && value == "민트색") return "민트색을 좋아하는구나! 연잎이랑 똑같은 색이야!";
                if (key == "Season" && value == "봄") return "봄을 좋아하는구나! 연못에 올챙이들이 꼬물꼬물해!";
            }
            if (kind.StartsWith("Bee") && key == "Food" && value == "사탕") return "사탕을 좋아하는구나! 나는 꿀도 좋아, 붕붕!";
            if (kind.StartsWith("Cat") && key == "Season" && value == "겨울") return "겨울을 좋아하는구나! 따뜻한 데서 낮잠 자기 딱 좋지…";
            return null;
        }

        /// <summary>A little line about what this friend itself loves, for chatting when clicked (null when it has none).</summary>
        public static string KindLine(string kind)
        {
            if (string.IsNullOrEmpty(kind)) return null;
            if (kind.StartsWith("Frog")) return "비 오는 날 연잎 위에 앉아 있으면 기분이 몽글몽글해, 개굴!";
            if (kind.StartsWith("Bunny")) return "깡총깡총! 오늘은 공원 끝까지 뛰어가 볼 거야!";
            if (kind.StartsWith("Cat")) return "햇볕 좋은 벤치는… 내 낮잠 자리야…";
            if (kind.StartsWith("Robot")) return "삐빅! 오늘 마을 행복 지수는 100점이에요!";
            if (kind.StartsWith("Car")) return "부릉부릉! 산책길 한 바퀴 누가 먼저 도나 내기할래?";
            if (kind.StartsWith("Dino")) return "크앙! 사실 나 무서운 공룡 아니야, 헤헤.";
            if (kind.StartsWith("Bee")) return "붕붕~ 꽃밭에서 꿀 냄새가 나!";
            if (kind.StartsWith("Plane")) return "관람차보다 더 높이 날아 보고 싶어, 슝!";
            if (kind.StartsWith("FireTruck")) return "마을은 오늘도 안전해! 내가 지키고 있거든.";
            if (kind.StartsWith("Pawn")) return "저, 저기… 오늘 날씨가 참 좋다…";
            return null;
        }

        static readonly string[] Cheers = { "새, 생일 축하해…! 오늘 정말 특별한 날이야!", "생일 축하해!! 오늘은 하루 종일 파티야!", "생일 축하해… 좋은 꿈 같은 하루 보내…", "생일 축하해! 소원은 뭐 빌 거야?", "생일 축하해. 태어나 줘서 고마워.", "생일 축하해! 깜짝 선물은 비밀이야, 헤헤!" };
        public static string BirthdayCheer(Personality p) => Cheers[P(p)];
    }
}
