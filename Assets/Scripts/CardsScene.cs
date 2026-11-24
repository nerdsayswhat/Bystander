using UnityEngine;
using UnityEngine.UI;

// Scene 4 of the Event List: the cards. Darkness isolates the player again and the spectacle is gone. Beat 1 (0 to 25 s): a card pops up in front,
// Stephen (the old man: the same blue particle figure) "was on his way to the hospital to visit his wife, who loves oranges". Beat 2 (25 to 65 s): Julia's card
// pops up on the right (the woman: the same purple figure), "on her way to give a job interview at her dream company"; then cards appear behind these two,
// ring after ring, spanning endlessly (60 cards in five rings, each with its own random story and a small particle person in a different colour).
// Beat 3 (65 to 80 s): the cards fade and, only if the chains were never broken, the words WERE THE CHAINS REALLY BINDING YOU? appear; the sequencer then
// fades out. Portraits are particle figures standing in a line-art card (the author, 2026-11-17: exactly the same model and colours as the old man and the
// woman, no photos), zoomed in and shown from the waist up (the particles below the hips are hidden). No sound yet.
// Follows SceneSequencer by step name ("4 ..."). Created in Scene 2.unity by Bystander > Create Scene 2 (which assigns the two standing idle clips).
public class CardsScene : MonoBehaviour
{
    public AnimationClip idleMan, idleWoman;                  // Mixamo standing idles, played in the portraits
    public float juliaPops = 12f, endlessFrom = 20f, chainsAt = 65f;   // seconds after the step is visible (Event List beats 1, 2 and 3)
    public float darkHold = 5f;                              // author 2026-11-24: seconds of darkness after the screen goes black, before the first card comes in (new field; the saved scene holds the older timings)
    float holdLeft;
    public float cardDist = 1.1f, juliaTurn = 38f;            // m in front; degrees to the right of Stephen
    public float figureZoom = 0.62f;                         // the figure inside a card: big, so that from the waist up it fills the portrait area
    public Color man = new Color(0.004f, 0.05f, 0.16f);       // the figure prefab's own azure
    public Color woman = new Color(0.06f, 0.012f, 0.16f);    // the same purple as WomanDogScene

    // the endless cards: x emission 6 gives these glowing colours that stay saturated (one channel clips, the others stay small)
    static readonly Color[] Palette =
    {
        new Color(0.008f, 0.083f, 0.27f), new Color(0.1f, 0.02f, 0.27f), new Color(0.15f, 0.05f, 0.007f), new Color(0.017f, 0.17f, 0.033f),
        new Color(0.008f, 0.17f, 0.15f), new Color(0.17f, 0.12f, 0.008f), new Color(0.17f, 0.025f, 0.1f), new Color(0.17f, 0.017f, 0.008f), new Color(0.12f, 0.12f, 0.13f)
    };

    // Every name and every story on the cards is unique (author 2026-11-23). A story has two parts: what the person was doing, and the problem that happened to them.
    // The eight themes below each hold 12 openers and 12 problems that fit them (8 x 12 x 12 = 1152 stories, about 940 are needed); the pairs are dealt out in one shuffled
    // order without repeating. The problems are verb phrases with no pronoun, so they follow any opener ("was carrying ... , but tripped on the kerb and ..."). The small far cards get
    // a short line (40 roles x 30 short problems = 1200, about 940 needed). A name is a first name only (author: no surname initial): the 940 cards with a full story all get different names (about 420 real ones, then made-up ones); the small far cards reuse real names (see BuildNames, NextName).
    static readonly string[][] Openers =
    {
        new[] {   // errands and deliveries
            "was carrying a box of books to the second-hand shop", "was bringing a casserole to a family who had just moved in", "was collecting a parcel from the post office",
            "was picking up a wedding ring from the jeweller", "was fetching bread and milk for the whole building", "was delivering a bouquet for a stranger",
            "was carrying a sapling to plant in the garden", "was buying saffron and lemons at the market", "was returning a borrowed coat to an old friend",
            "was posting a letter to a prisoner", "was bringing a lunch box a child had left behind", "was taking sandwiches to the night guard" },
        new[] {   // appointments, tests and chances
            "was heading to a job fair with a freshly printed CV", "was taking a driving test that afternoon", "was going to hand in a thesis a day early",
            "had been practising a speech for the town council", "was on the way to the last exam of the year", "was meeting a lawyer about a lost inheritance",
            "was heading to the first rehearsal of the choir", "was going to sign the papers on a tiny bakery", "was on the way to a blood test, trying not to worry",
            "was on the way to enrol at night school", "was walking to the dentist and dreading it", "was on the way to vote for the first time" },
        new[] {   // visits and family
            "was on the way to see the grandchildren after a long journey", "was going to meet a son not seen in years", "was heading to the airport to surprise the parents",
            "was walking to the station to meet a twin brother", "was on the way to apologise to a brother", "was visiting an old teacher in hospital",
            "was on the way to see a pen friend of thirty years", "was bringing apples to the school for harvest day", "was going to see the sea for the first time in ten years",
            "was walking a friend home after a bad day", "was on the way to share big news with the boss", "had decided today was the day to call an old friend" },
        new[] {   // making things and learning
            "was learning the cello and carrying it to a lesson", "was practising a magic trick for a niece", "was carrying paintings to a first exhibition",
            "was practising a language on a lunch break", "was carrying a guitar to the open mic night", "was learning to juggle in the park",
            "was singing quietly on the way to choir", "was taking photographs of the street for a friend overseas", "was heading to a pottery class, nervous and excited",
            "was on the way to a night class in carpentry", "was sketching the harbour boats for a first exhibition", "was carrying a kite to the park for a little sister" },
        new[] {   // care and health
            "was bringing soup to a sick neighbour", "was going to the pharmacy for a father's medicine", "was taking a stray kitten to the vet",
            "was rushing a sick child to the clinic", "was bringing blankets to the shelter on the corner", "was on the way to donate blood for the first time",
            "was fetching a ladder to fix a neighbour's roof", "was taking the first walk since the operation", "had just finished chemotherapy and was walking home slowly",
            "was walking an old family dog for the last time", "was carrying water to the old couple upstairs", "was bringing a warm meal to a friend who had lost a job" },
        new[] {   // travelling and commuting
            "was running to catch a ferry to see a grandmother", "was racing for the bus with a thermos of tea", "had missed the last bus and decided to walk home",
            "was cycling to a late shift at the hospital", "was on the way to a football match with a son", "was on the way to the harbour to watch the boats",
            "was walking to work for the first time after a year at home", "was reading a map upside down and laughing about it", "was heading to the market before dawn",
            "was on the way to a retirement party for a colleague", "was buying a ticket to a city never seen before", "was learning the way to a new school" },
        new[] {   // happy moments
            "had just quit a job and felt strangely light", "had just sold a first painting", "had just passed an exam on the third attempt",
            "had just been offered a job and could not stop smiling", "had saved all year to buy a warm coat for a mother", "was waiting for a call about an adoption",
            "was telling a friend about a dream on the phone", "had just heard a song that was a reminder of home", "was on the way to propose",
            "was going home to cook for the first time in a month", "was on the phone telling a mother about passing the test", "was on a first date, rehearsing a joke" },
        new[] {   // an ordinary day in the neighbourhood
            "was running errands for the whole building", "was walking off an argument with a sister", "was lost and looking for the library",
            "was jogging to get fit after a long illness", "was meeting the neighbour who had saved a cat", "was on the way to light a candle in the old church",
            "was on the way to the mosque to meet a father", "was going to a funeral in the only black dress", "was on the way to scatter ashes by the water",
            "was heading to the gym for the first time in years", "was on the way to confess a small lie", "was walking to the bridge to watch the sunset" },
    };
    // The problems are written to fit EVERY opener of every theme (author 2026-11-24: the first half of each story must make sense with the second half): they name no object, no place, no
    // vehicle and no pronoun, only what happened to the person (a fall, a theft, panic, cruelty, grief), so any pairing reads naturally. The themes only shift the tone.
    static readonly string[][] Problems =
    {
        new[] {   // errands and deliveries
            "but tripped on the kerb and could not get up", "until a stranger snatched everything and ran", "but a sudden dizzy spell ended it on the pavement",
            "until a sharp pain in the chest forced a long rest on the ground", "but was knocked down by a cyclist and left with a bruised shoulder", "until a bad fall left a swollen ankle and a long wait for help",
            "but was laughed at by passers-by and wished to disappear", "until the crowd walked on without a glance after a fall", "but slipped on wet leaves and sprained an ankle",
            "until panic rose from nowhere and made every step impossible", "but was robbed in the street and left with nothing", "until a cramp locked the leg and every step turned to pain" },
        new[] {   // appointments, tests and chances
            "but a sudden wave of panic emptied the mind", "but now lacks the confidence to go through with it", "until a fall on the steps left a swollen knee and everything ruined",
            "but the fear of failing came back and refused to leave", "until a stranger's cruel remark made every plan feel foolish", "but froze on the spot and could not take another step",
            "until the shaking in both hands would not stop", "but a long, anxious wait wore the courage away", "until a dizzy spell forced a stop and the chance slipped away",
            "but shame took over and ended the hope", "until news of a death in the family broke the day in two", "but a cold sweat and a racing heart took over the whole day" },
        new[] {   // visits and family
            "but fell on the stairs and lay there waiting for help", "until a stranger's anger left every kind word unsaid", "but the old fear of being a burden took hold",
            "until a heart flutter forced a long sit on the cold ground", "but nobody would stop to help after a bad fall", "until the sudden loss of a loved one broke every plan",
            "but doubt crept in, sure that no one wanted this", "until a dizzy turn made the street spin and the day was lost", "but was pushed aside in the crowd and hurt a wrist",
            "until a storm of tears made it impossible to go on", "but loneliness arrived all at once and stayed", "until a quiet disappointment took the whole day" },
        new[] {   // making things and learning
            "but a taunt from a passer-by took away all courage", "until a careless crowd pushed past and left bruises", "but old self-doubt returned and froze every plan",
            "until a stranger sneered and the joy went out of it", "but a tumble left a broken wrist and months of recovery", "until the rain soaked everything and the spirit sank",
            "but the nerve vanished at the last moment and shame took over", "until someone mocked the effort and it stopped for good", "but a sudden panic took hold and ended it all",
            "until a trembling hand gave out and all effort stopped", "but a fall in front of strangers took away the nerve", "until a shout from behind made the heart race and the day was lost" },
        new[] {   // care and health
            "but collapsed on the pavement and no one stopped", "until a bad fall left an hour of lying alone", "but could not bend or lift, and no one came to help",
            "until a stranger's shout set the hands shaking and the effort failed", "but a wave of exhaustion made every step heavy", "until a sharp cramp locked the leg and ended the effort",
            "but a sudden faint ended it on the ground, alone", "until pain from an old injury returned and stopped everything", "but a dog's bark set off a panic and a bad fall",
            "until strength gave out and the effort felt wasted", "but tears came without warning and would not stop", "until the heat made everything swim and the legs gave out" },
        new[] {   // travelling and commuting
            "but slipped on the steps and was left shaken", "until a bike clipped a hip and left a deep bruise", "but was robbed of everything in a moment",
            "until a panic in the crowd left a frightened, lonely feeling", "but a twisted ankle ended it on a cold bench", "until a wrong turn led somewhere unsafe and unwelcoming",
            "but was ignored by everyone who was asked for help", "until the phone died and no one could be reached", "but fell hard and could not stand",
            "until a stranger's temper soured the whole day", "but an old knee gave out for good", "until a sudden storm soaked everything and shook the nerve" },
        new[] {   // happy moments
            "but the joy turned to dread when a silence fell", "until a cruel message wiped out the whole feeling", "but the happiness was lost in a fall that no one stopped for",
            "until a stranger's cutting words made it all feel undeserved", "but then the nerve was lost and the moment slipped by", "until a dizzy spell left a long, shaking wait on the ground",
            "but a sudden bout of grief swallowed every other feeling", "until a sudden wave of sadness took the happiness away", "but then a fall turned the laughter to shame",
            "until a theft took the money and the mood", "but found no one to share it with, and the happiness felt empty", "until a sharp fear of being alone arrived and stayed" },
        new[] {   // an ordinary day in the neighbourhood
            "but a push in the crowd knocked everything over and no one paused", "until a shouted insult made every step shrink", "but the old illness returned and cut the day short",
            "until the fear of being watched made every step heavy", "but a stumble in front of strangers brought laughter and shame", "until a fall left a gash that bled onto the pavement",
            "but found every door locked and sat down, defeated", "until panic took hold and breathing would not slow", "but was shoved aside by a hurrying crowd and cried",
            "until a stranger's cold stare undid the last of the courage", "but a cramp in the chest forced a long rest on the kerb", "until a sudden dizziness blurred everything and the knees folded" },
    };
    static readonly string[] Roles =
    {
        "a baker", "a nurse", "a student", "a bus driver", "a gardener", "a teacher", "a cook", "a cleaner", "a painter", "a courier", "a barber", "a tailor", "a waiter", "a mechanic",
        "a pianist", "a poet", "a dancer", "a welder", "a judge", "a nanny", "a florist", "a pilot", "a sailor", "a clerk", "a lawyer", "an actor", "a farmer", "a librarian", "a butcher",
        "a potter", "a plumber", "a carpenter", "a dentist", "a porter", "a runner", "a fisher", "a priest", "a chef", "a miner", "a guard"
    };
    static readonly string[] ShortProblems =
    {
        ", hurt in a fall", ", lost a job today", ", robbed on the way", ", shaken by a stranger", ", sick and alone", ", ignored when falling", ", missed a last chance",
        ", lost all confidence", ", mocked in public", ", frozen by panic", ", too tired to go on", ", hit by a cyclist", ", a day ruined by cruelty", ", dropped everything",
        ", cried on a bench", ", lost and frightened", ", turned away at the door", ", afraid to try again", ", bruised and alone", ", broke a wrist", ", fainted in the street",
        ", lost the way home", ", shaking with fear", ", unseen when in need", ", fell and lay there", ", sneered at", ", nothing left to give", ", heartbroken", ", too ashamed to speak"
    };
    static readonly string[] FirstNames =
    {
        "Maria", "Tom", "Aisha", "Ben", "Chen", "Elena", "Omar", "Priya", "Jack", "Lucia", "Yusuf", "Hannah", "Diego", "Mei", "Samuel", "Nora", "Ravi", "Ingrid", "Kofi", "Sofia",
        "Leo", "Fatima", "Pedro", "Anna", "Hiro", "Zara", "Marcus", "Ines", "Andre", "Leila", "Amara", "Bruno", "Camila", "Dmitri", "Esther", "Felix", "Grace", "Hassan", "Irene", "Jamal",
        "Katya", "Liam", "Mina", "Nikolai", "Olga", "Paolo", "Quentin", "Rosa", "Sami", "Tara", "Umar", "Vera", "Wei", "Ximena", "Yara", "Zoltan", "Abel", "Bianca", "Carlos", "Dalia",
        "Emre", "Freya", "Gabriel", "Hana", "Isaac", "Jelena", "Karim", "Lena", "Mateo", "Naomi", "Oscar", "Paloma", "Rafael", "Salma", "Theo", "Uma", "Viktor", "Wanda", "Xavier", "Yasmin",
        "Zainab", "Aaron", "Bella", "Cyrus", "Dara", "Eli", "Farah", "Gustavo", "Helena", "Idris", "Joana", "Kenji", "Lola", "Malik", "Nadia", "Otto", "Petra", "Rashid", "Selma", "Tariq",
        "Ulla", "Valeria", "Waleed", "Yuki", "Zeynep", "Arjun", "Beatriz", "Cora", "Dev", "Elif", "Fabian", "Gita", "Hugo", "Ivana", "Jonas", "Kira", "Luca", "Mara", "Niels", "Odette",
        "Pablo", "Reza", "Sunita", "Tomas", "Ursula", "Vikram", "Wen", "Yolanda", "Zack", "Alma"
    };
    // more real first names (the list above plus this one, de-duplicated in BuildNames); the rest of the 3,000 or so names a full pile needs are made up from syllables, so
    // the cards nearest the player, which are dealt first, carry real names and every name in the whole pile is still different
    static readonly string[] MoreNames =
    {
        "Adam", "Adriana", "Agnes", "Ahmed", "Aiden", "Akira", "Alba", "Alberto", "Alec", "Alex", "Alexei", "Alice", "Alicia", "Alina", "Alva", "Amelia", "Amir", "Ana", "Anastasia", "Anders",
        "Andrea", "Angel", "Angela", "Anil", "Anita", "Anton", "Antonia", "Archie", "Ariana", "Arthur", "Asha", "Astrid", "Ayesha", "Beata", "Benedict", "Bernard", "Bertha", "Bilal", "Blanca",
        "Boris", "Brenda", "Bridget", "Caleb", "Callum", "Camille", "Carla", "Carmen", "Cassandra", "Cecilia", "Cedric", "Celia", "Chiara", "Chloe", "Christian", "Clara", "Claude", "Clement",
        "Colin", "Connor", "Cristina", "Daniel", "Daria", "David", "Deborah", "Denis", "Derek", "Devi", "Dina", "Dominic", "Dora", "Dorothy", "Edgar", "Edith", "Edward", "Eleni", "Eliza",
        "Ella", "Elsa", "Emil", "Emma", "Enzo", "Erik", "Erin", "Ernesto", "Eva", "Evelyn", "Ezra", "Fabio", "Faisal", "Fiona", "Florence", "Francesca", "Frank", "Frida", "Gareth", "Gemma",
        "George", "Gerda", "Gianni", "Gloria", "Gordon", "Greta", "Gwen", "Hamid", "Harold", "Harper", "Hazel", "Heidi", "Henry", "Hilda", "Hope", "Ian", "Ida", "Iliana", "Imani", "Ingo",
        "Isabel", "Isla", "Ivan", "Ivy", "Jacob", "Jade", "Jana", "Jasmine", "Javier", "Jean", "Jenny", "Jerome", "Jessica", "Joel", "Johan", "Jorge", "Josef", "Josephine", "Juan", "Judith",
        "Julian", "Juno", "Justin", "Karen", "Karl", "Katarina", "Kate", "Keiko", "Kemal", "Kevin", "Khalid", "Klara", "Konrad", "Kwame", "Lars", "Laura", "Layla", "Leah", "Leonard", "Leona",
        "Lily", "Linda", "Lionel", "Lisa", "Liv", "Lorenzo", "Louis", "Lucas", "Lucy", "Ludmila", "Luis", "Lukas", "Lydia", "Mabel", "Maggie", "Magnus", "Mahmoud", "Maja", "Manuel", "Marco",
        "Margaret", "Margot", "Marie", "Mario", "Marta", "Martin", "Mary", "Matilda", "Maurice", "Max", "Maya", "Megan", "Melina", "Mercy", "Michael", "Miguel", "Mila", "Miles", "Miriam",
        "Mohammed", "Monica", "Murat", "Nadir", "Nancy", "Natalia", "Nathan", "Neil", "Nell", "Nicolas", "Nina", "Noah", "Noel", "Nuria", "Olivia", "Omari", "Orla", "Oswald", "Owen", "Pam",
        "Patrick", "Paula", "Pearl", "Penelope", "Peter", "Philip", "Pia", "Piotr", "Polly", "Rachel", "Ramon", "Raul", "Rebecca", "Regina", "Remy", "Renata", "Rhea", "Richard", "Rita",
        "Robert", "Roberto", "Rodrigo", "Roman", "Rory", "Ruben", "Ruth", "Sabine", "Sade", "Sally", "Sara", "Saul", "Sebastian", "Serena", "Sergio", "Seth", "Shira", "Sigrid", "Silvia",
        "Simon", "Sonia", "Stella", "Susan", "Svetlana", "Tamar", "Tania", "Teresa", "Thomas", "Tilda", "Timothy", "Tobias", "Tristan", "Ulrich", "Valentina", "Vanessa", "Vincent", "Violet",
        "Vlad", "Walter", "Willem", "William", "Yael", "Yasir", "Yvonne", "Zachary", "Zahra", "Zelda", "Zoe", "Zuri"
    };
    static readonly string[] Onsets = { "b", "d", "f", "g", "h", "j", "k", "l", "m", "n", "p", "r", "s", "t", "v", "z", "br", "dr", "kr", "tr", "ch", "sh", "th", "gr", "pl", "sv", "kl" };
    static readonly string[] Vowels = { "a", "e", "i", "o", "u", "a", "e", "o", "ia", "ei" };
    static readonly string[] Ends = { "a", "an", "o", "el", "is", "ar", "en", "ia", "ya", "ik", "im", "on", "us", "e", "i", "in", "as", "ora", "ina", "ek", "av", "ir", "ana", "ko", "ul", "ev", "ay" };
    static readonly string[] Blocked = { "tit", "ass", "fuk", "sex", "nig", "cum", "dik", "fag", "poo", "shit", "kkk", "nazi", "hell", "dam", "gay" };
    static System.Collections.Generic.List<string> names; static int realCount; const int UniqueNames = 940;   // 400 near + 220 + 320 far cards read as full stories: all different
    static void BuildNames()
    {
        var seen = new System.Collections.Generic.HashSet<string> { "Stephen", "Julia" };       // their cards are the two real ones
        var real = new System.Collections.Generic.List<string>();
        foreach (var n in FirstNames) if (seen.Add(n)) real.Add(n);
        foreach (var n in MoreNames) if (seen.Add(n)) real.Add(n);
        var rr = new System.Random(13);
        for (int i = real.Count - 1; i > 0; i--) { int j = rr.Next(i + 1); var t = real[i]; real[i] = real[j]; real[j] = t; }
        names = real; realCount = real.Count;
        var g = new System.Random(5);
        while (names.Count < UniqueNames)                                                           // the cards with a full story (the 400 near ones and the first two far bands) each get their own name
        {
            string s = Onsets[g.Next(Onsets.Length)] + Vowels[g.Next(Vowels.Length)] + (g.Next(2) == 0 ? Onsets[g.Next(Onsets.Length)] + Vowels[g.Next(Vowels.Length)] : "") + Ends[g.Next(Ends.Length)];
            if (s.Length < 4 || s.Length > 9) continue;
            bool bad = false; foreach (var b in Blocked) if (s.Contains(b)) { bad = true; break; }
            s = char.ToUpper(s[0]) + s.Substring(1);
            if (!bad && seen.Add(s)) names.Add(s);
        }
    }
    static System.Collections.Generic.List<int> longOrder, shortOrder; static int longI, shortI, nameI;
    static void ResetStories()
    {
        longI = shortI = nameI = 0;
        int per = Openers[0].Length * Problems[0].Length, total = Openers.Length * per;               // pair k: theme k / per, opener (k % per) / problems, problem (k % per) % problems
        longOrder = Shuffled(total, 11); shortOrder = Shuffled(Roles.Length * ShortProblems.Length, 12);
        if (names == null) BuildNames();
    }
    static System.Collections.Generic.List<int> Shuffled(int n, int seed)
    {
        var l = new System.Collections.Generic.List<int>(n); for (int i = 0; i < n; i++) l.Add(i);
        var r = new System.Random(seed);
        for (int i = n - 1; i > 0; i--) { int j = r.Next(i + 1); int tmp = l[i]; l[i] = l[j]; l[j] = tmp; }
        return l;
    }
    static string NextLong()
    {
        int k = longOrder[longI++ % longOrder.Count], per = Openers[0].Length * Problems[0].Length, th = k / per, r = k % per, np = Problems[th].Length;
        return Openers[th][r / np] + ", " + Problems[th][r % np] + ".";
    }
    static string NextShort() { int k = shortOrder[shortI++ % shortOrder.Count]; return Roles[k / ShortProblems.Length] + ShortProblems[k % ShortProblems.Length]; }
    // first names only. The first 940 (every card with a full story) are all different: the real names first, then made-up ones if the real ones run out. The small far cards
    // (short line or name only, too small to read) reuse real names, so the card count is unchanged.
    static string NextName() { int i = nameI++; return i < UniqueNames ? names[i] : names[(i * 7919) % realCount]; }

    struct Band { public float r0, r1, dim; public int count, people, chars; public bool glass; }
    static readonly Band[] Far =                                                          // the cards beyond the readable ones: distance, brightness, count, particles per person, glass
    {
        new Band { r0 = 8f,   r1 = 13f,  dim = 0.55f, count = 220, people = 18, chars = 22, glass = true },
        new Band { r0 = 13f,  r1 = 20f,  dim = 0.34f, count = 320, people = 12, chars = 22, glass = true },
        new Band { r0 = 20f,  r1 = 32f,  dim = 0.20f, count = 420, people = 9,  chars = 22, glass = false },
        new Band { r0 = 32f,  r1 = 50f,  dim = 0.12f, count = 520, people = 7,  chars = 22, glass = false },
        new Band { r0 = 50f,  r1 = 80f,  dim = 0.08f, count = 600, people = 6,  chars = 0, glass = false },
        new Band { r0 = 80f,  r1 = 120f, dim = 0.05f, count = 700, people = 6,  chars = 0, glass = false },
    };

    static float Halton(int index, int b)
    {
        float f = 1f, r = 0f;
        for (int i = index; i > 0; i /= b) { f /= b; r += f * (i % b); }
        return r;
    }

    // URP Unlit set to Transparent (alpha blend, no depth write), made in code so that the shader that ships with the chain material is the one used
    static Material glassMat;
    static Material GlassMaterial()
    {
        if (glassMat != null) return glassMat;
        glassMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        glassMat.SetFloat("_Surface", 1f); glassMat.SetFloat("_Blend", 0f);
        glassMat.SetOverrideTag("RenderType", "Transparent");
        glassMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha); glassMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        glassMat.SetInt("_ZWrite", 0); glassMat.SetInt("_Cull", 0);
        glassMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); glassMat.renderQueue = 3000;
        return glassMat;
    }

    enum Phase { Idle, Shown }
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    SceneSequencer seq; Transform head; Phase phase; float t, findTimer, endlessLit, chainsLit; bool warned;
    Vector3 origin, fwd;
    Card stephen, julia; GameObject endless; MaterialPropertyBlock mpb, mpb2; Text chainsText; GameObject chainsGo;
    public int cardCount = 400, waves = 4; public float waveEvery = 7f;                  // the endless cards come in waves so that the pile keeps growing
    Wave[] wave; Vector3 eye0;

    class Wave
    {
        public float at, lit, dim = 1f; public int band = -1; public bool built = true; public MeshRenderer frames, fills; public System.Collections.Generic.List<Vector3> fverts = new System.Collections.Generic.List<Vector3>(); public System.Collections.Generic.List<int> ftris = new System.Collections.Generic.List<int>(); public ParticleSystemRenderer people; public Material mat;
        public System.Collections.Generic.List<Vector3> cpos = new System.Collections.Generic.List<Vector3>();   // where every card of the wave sits (the music plays its notes there)
        public System.Collections.Generic.List<float> psz = new System.Collections.Generic.List<float>(); public CanvasGroup texts; public Transform canvas;           // all the stories of a wave sit on one canvas, so they batch into few draw calls
        public System.Collections.Generic.List<Vector3> verts = new System.Collections.Generic.List<Vector3>(), pp = new System.Collections.Generic.List<Vector3>();
        public System.Collections.Generic.List<int> tris = new System.Collections.Generic.List<int>(); public System.Collections.Generic.List<Color> pc = new System.Collections.Generic.List<Color>();
    }

    class Card
    {
        public GameObject root; public CanvasGroup group; public ParticleFigure fig; public float shown; public Vector3 pos; public Vector3 face; public bool placed, fitted, corrected;
        public AnimationClip clip; public Color color; public float waistY;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Spawn()
    {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Scene 2" && FindFirstObjectByType<CardsScene>() == null)
            new GameObject("CardsScene").AddComponent<CardsScene>();
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if ((findTimer -= dt) <= 0f)
        {
            findTimer = 1f;
            if (seq == null) seq = FindFirstObjectByType<SceneSequencer>();
            if (head == null && Camera.main != null) head = Camera.main.transform;
        }
        bool want = seq != null && head != null && seq.Index >= 0 && seq.steps[seq.Index].name.StartsWith("4");
        if (want && phase == Phase.Idle && !seq.Busy) Begin();
        if (!want && phase != Phase.Idle) { End(); return; }
        if (phase == Phase.Idle) return;

        if (holdLeft > 0f) { holdLeft -= dt; return; }                                   // the darkness stays for darkHold seconds before the first card
        t += dt;
        PlayMusic();
        Pop(stephen, 0f); if (t >= juliaPops) Pop(julia, juliaPops);
        float fadeOut = Mathf.Clamp01((t - chainsAt) / 2f);                             // the cards fade as the last beat begins
        if (wave != null)
            foreach (var w in wave)
            {
                if (!w.built) { if (t >= w.at - 1.5f) BuildBand(w); else continue; }
                w.lit = Mathf.MoveTowards(w.lit, Mathf.Clamp01((t - w.at) / 6f) * (1f - fadeOut), dt);
                bool on = w.lit > 0.01f;
                w.frames.enabled = on;
                mpb.SetColor(BaseColor, Color.white * 0.9f * w.dim * w.lit); w.frames.SetPropertyBlock(mpb);
                if (w.fills != null) { w.fills.enabled = on; mpb2.SetColor(BaseColor, new Color(0.25f, 0.45f, 0.75f, 0.07f * w.lit)); w.fills.SetPropertyBlock(mpb2); }
                if (w.people != null) { w.people.enabled = on; w.mat.SetColor(BaseColor, Color.white * 6f * w.dim * w.lit); }
                if (w.texts != null) w.texts.alpha = w.lit * 0.85f * w.dim;
            }
        Fade(stephen, 1f - fadeOut); Fade(julia, 1f - fadeOut);
        if (chainsGo != null)
        {
            chainsLit = Mathf.MoveTowards(chainsLit, t >= chainsAt + 1f ? 1f : 0f, dt * 0.7f);
            chainsText.color = new Color(1f, 1f, 1f, chainsLit);
        }
    }

    // ---- the music of the cards (author 2026-11-24, approved draft 6: SoundReview/cards_medley_instruments_v6_draft.wav) -------------------------------------------------
    // Every layer brings a new instrument, and every note is DIRECTIONAL: it plays at the position of a card (3D, distance based). Card 1 a music box (one note), card 2 a soft piano
    // (one note), layer 1 a slow harp arpeggio (11 notes), layer 2 a slow vibraphone phrase (13 notes), layer 3 strings and layer 4 a choir (one note for each card of the layer, 100),
    // a fine glockenspiel shimmer for each far band, a quiet piano and flute melody at the end, and a soft pad (Bm G D A) from two sides underneath. The notes are files in
    // Assets/Resources/Audio: cn_<instrument>_<d|m><degree> (d = degree 0 and up, m = below: cn_strings_m3), degrees of the D major pentatonic scale (0 = D4, 5 = D5).
    struct MEv { public float at; public string clip; public Vector3 pos; public float vol; }
    readonly System.Collections.Generic.List<MEv> music = new System.Collections.Generic.List<MEv>(); int musicI; bool padStarted;
    readonly System.Collections.Generic.List<Sfx.Handle> padHs = new System.Collections.Generic.List<Sfx.Handle>();
    static string Cn(string inst, int d) => "cn_" + inst + "_" + (d < 0 ? "m" : "d") + Mathf.Abs(d);
    Vector3 CardPos(int w, int i) { var l = wave != null && w < wave.Length ? wave[w].cpos : null; return l != null && l.Count > 0 ? l[i % l.Count] : head.position + Quaternion.Euler(0f, i * 137.5f, 0f) * Vector3.forward * 3f; }
    void BuildMusic()
    {
        music.Clear(); musicI = 0; padStarted = false; var r = new System.Random(99);
        void Add(float at, string clip, Vector3 pos, float vol) => music.Add(new MEv { at = at, clip = clip, pos = pos, vol = vol });
        if (stephen != null) Add(0.5f, Cn("musicbox", 11), stephen.root.transform.position, 0.9f);                                   // card 1: one note
        if (julia != null) Add(juliaPops + 0.5f, Cn("piano", 7), julia.root.transform.position, 0.9f);                              // card 2: one note
        var harp = new (int d, float dt, float v)[] { (5, 0f, .8f), (7, .55f, .65f), (8, 1.1f, .7f), (10, 1.75f, .85f), (8, 2.4f, .6f), (7, 3f, .6f), (5, 3.6f, .7f), (8, 4.3f, .65f), (7, 5f, .55f), (5, 5.7f, .6f), (3, 6.4f, .5f) };
        for (int k = 0; k < harp.Length; k++) Add(wave[0].at + harp[k].dt + (float)(r.NextDouble() - .5) * 0.06f, Cn("harp", harp[k].d), CardPos(0, r.Next(1000)), harp[k].v);   // layer 1
        var vib = new (int d, float dt, float v)[] { (3, 0f, .8f), (5, .6f, .7f), (6, 1.2f, .75f), (8, 1.9f, .85f), (6, 2.6f, .7f), (5, 3.2f, .65f), (8, 3.9f, .8f), (10, 4.7f, .9f), (9, 5.5f, .8f), (8, 6.2f, .7f), (6, 7f, .7f), (5, 7.8f, .6f), (3, 8.6f, .55f) };
        for (int k = 0; k < vib.Length; k++) Add(wave[1].at + vib[k].dt, Cn("vibes", vib[k].d), CardPos(1, r.Next(1000)), vib[k].v * 0.9f);                                   // layer 2
        for (int layer = 2; layer <= 3; layer++)                                                                                       // layers 3 and 4: one note for every card of the layer
        {
            string inst = layer == 2 ? "strings" : "choir"; int lo = layer == 2 ? -5 : -3, hi = layer == 2 ? 5 : 7, n = Mathf.Max(wave[layer].cpos.Count, 1), d = (lo + hi) / 2;
            var ts = new System.Collections.Generic.List<float>(); for (int k = 0; k < n; k++) ts.Add(6.5f * Mathf.Pow((float)r.NextDouble(), 0.8f)); ts.Sort();
            int[] steps = { -2, -1, 1, 1, 2, 3 };
            for (int k = 0; k < n; k++) { d = Mathf.Clamp(d + steps[r.Next(steps.Length)], lo, hi); Add(wave[layer].at + ts[k], Cn(inst, d), CardPos(layer, k), 0.4f * Mathf.Lerp(0.45f, 1f, (float)r.NextDouble())); }
        }
        for (int b = 0; b < Far.Length; b++)                                                                                            // the far bands: a fine shimmer, 14 notes each, far away in random directions
            for (int k = 0; k < 14; k++) Add(endlessFrom + 4f + b * 5f + (float)r.NextDouble() * 4f, Cn("glock", r.Next(14, 21)), head.position + Quaternion.Euler(0f, (float)r.NextDouble() * 360f, 0f) * Vector3.forward * Mathf.Lerp(9f, 20f, (float)r.NextDouble()) + Vector3.up * Mathf.Lerp(-0.5f, 2f, (float)r.NextDouble()), 0.3f);
        var flat = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized; var mel = head.position + flat * 2.5f + Vector3.up * 0.3f;   // the closing melody: ahead of the player
        float[] mt = { 47f, 49f, 51f, 53.5f, 56f, 58.5f, 61f }; int[] md = { 13, 11, 10, 8, 10, 8, 6 };
        for (int k = 0; k < mt.Length; k++) { Add(mt[k], Cn("piano", md[k]), mel + Vector3.left * 0.3f, 0.7f); Add(mt[k] + 0.05f, Cn("flute", Mathf.Min(md[k] + 5, 17)), mel + Vector3.right * 0.3f, 0.45f); }
        music.Sort((a, b) => a.at.CompareTo(b.at));
    }
    void PlayMusic()
    {
        if (!padStarted)                                                                                                                // the pad starts with the first card, from a left and a right source behind the player
        {
            padStarted = true; var flat = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized; var rt = Vector3.Cross(Vector3.up, flat);
            foreach (float side in new[] { -1f, 1f }) { var h = Sfx.Loop("cards_pad", head.position - flat * 2f + rt * side * 4f, 0.3f, true, 30f, 3f); padHs.Add(h); }
        }
        while (musicI < music.Count && t >= music[musicI].at) { var e = music[musicI++]; Sfx.Play(e.clip, e.pos, e.vol, true, 40f, 1.5f); }
    }
    void StopMusic() { foreach (var h in padHs) h.Stop(); padHs.Clear(); music.Clear(); musicI = 0; padStarted = false; }

    void Begin()
    {
        if (idleMan == null)
        {
            if (!warned) { warned = true; Debug.LogWarning("CardsScene: the portrait clips are not assigned (use Bystander > Create Scene 2); the cards show without figures"); }
        }
        t = 0f; endlessLit = chainsLit = 0f; ResetStories(); holdLeft = darkHold;
        fwd = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
        origin = new Vector3(head.position.x, 0f, head.position.z);
        float cy = head.position.y - 0.12f;                                              // the card's centre, a little below the eyes
        stephen = MakeCard("Stephen", "<b>STEPHEN</b>\nwas getting oranges for his wife, who loves them, until he lost his balance because of his age.", Quaternion.identity, cy, idleMan, man, false);
        julia = MakeCard("Julia", "<b>JULIA</b>\nwas on her way to a job interview at her dream company, but now lacks the confidence to go.", Quaternion.Euler(0f, juliaTurn, 0f), cy, idleMan, woman, true);   // every character plays Stephen's idle
        BuildEndless(cy); BuildMusic();
        if (seq.ShowChainsLine) BuildChainsLine();
        phase = Phase.Shown;
        Log("scene 4 cards begin");
    }

    // a line-art card with a particle portrait (waist up) above its text; hidden until Pop
    Card MakeCard(string name, string story, Quaternion turn, float cy, AnimationClip clip, Color color, bool female)
    {
        var c = new Card { clip = clip, color = color };
        c.pos = origin + turn * fwd * cardDist; c.pos.y = cy;
        c.face = head.position - c.pos; c.face.y = 0f; c.face.Normalize();
        var go = new GameObject("Card " + name, typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
        go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        var rt = (RectTransform)go.transform; rt.sizeDelta = new Vector2(500f, 760f);
        go.transform.position = c.pos; go.transform.rotation = Quaternion.LookRotation(-c.face); go.transform.localScale = Vector3.one * 0.001f;
        c.group = go.GetComponent<CanvasGroup>(); c.group.alpha = 0f; c.group.blocksRaycasts = false;
        c.root = go;
        // glass: a faint tint of the person's colour and a rounded outline
        var tint = new Color(Mathf.Clamp01(color.r * 6f), Mathf.Clamp01(color.g * 6f), Mathf.Clamp01(color.b * 6f), 0.07f);
        var gl = new GameObject("Glass", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        gl.transform.SetParent(go.transform, false);
        var gr = (RectTransform)gl.transform; gr.anchorMin = Vector2.zero; gr.anchorMax = Vector2.one; gr.offsetMin = gr.offsetMax = Vector2.zero;
        gl.sprite = FillSprite(); gl.type = Image.Type.Sliced; gl.color = tint; gl.raycastTarget = false;
        var fr = new GameObject("Frame", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        fr.transform.SetParent(go.transform, false);
        var fr2 = (RectTransform)fr.transform; fr2.anchorMin = Vector2.zero; fr2.anchorMax = Vector2.one; fr2.offsetMin = fr2.offsetMax = Vector2.zero;
        fr.sprite = FrameSprite(); fr.type = Image.Type.Sliced; fr.color = new Color(1f, 1f, 1f, 0.55f); fr.raycastTarget = false;
        var tx = new GameObject("Text", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
        tx.transform.SetParent(go.transform, false);
        var r = (RectTransform)tx.transform; r.sizeDelta = new Vector2(440f, 190f); r.anchoredPosition = new Vector2(0f, -283f);
        tx.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); tx.fontSize = story.Length > 90 ? 27 : 32; tx.alignment = TextAnchor.MiddleCenter; tx.supportRichText = true;
        tx.horizontalOverflow = HorizontalWrapMode.Wrap; tx.verticalOverflow = VerticalWrapMode.Overflow; tx.raycastTarget = false;
        tx.text = story; tx.color = Color.white;
        c.waistY = cy - 0.12f;                                                           // the figure is cut here, just above the text
        if (clip != null)
        {
            var prefab = Resources.Load<GameObject>("Prefab/ParticleFigure");
            var fg = Instantiate(prefab); fg.name = "Portrait " + name;
            var f = fg.GetComponent<ParticleFigure>();
            BodyLibrary.Apply(f, female);                                                   // the same real body surfaces as the old man and the lady (if baked); the sizes below are set after it, so they stay as before
            f.clip = clip; f.loop = true; f.color = color; f.particleCount = 1600; f.sizeMin = 0.004f; f.sizeMax = 0.011f; f.particleSize = 0.006f;
            f.position = new Vector3(c.pos.x, 0f, c.pos.z) + c.face * 0.04f;               // a little in front of the card
            f.groundY = c.waistY - 0.6f;                                                    // refined once the hips are known (Pop)
            f.cutBelowY = c.waistY - 0.03f;                                                 // only the figure from the waist up is drawn
            f.brightness = 0f;
            c.fig = f;
        }
        return c;
    }

    static Sprite frameSprite;
    // a 96 x 96 rounded-rectangle outline (radius 20, line 5), antialiased; used 9-sliced with a border of 24 (the glass fill is the same shape, solid)
    static Sprite FrameSprite()
    {
        if (frameSprite != null) return frameSprite;
        const int n = 96; const float rad = 20f, line = 5f;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = Mathf.Abs(x + 0.5f - n / 2f) - (n / 2f - rad - 1f), dy = Mathf.Abs(y + 0.5f - n / 2f) - (n / 2f - rad - 1f);
                float d = Mathf.Sqrt(Mathf.Max(dx, 0f) * Mathf.Max(dx, 0f) + Mathf.Max(dy, 0f) * Mathf.Max(dy, 0f)) + Mathf.Min(Mathf.Max(dx, dy), 0f) - rad;   // signed distance to the rounded rectangle
                float a = Mathf.Clamp01(line * 0.5f + 0.5f - Mathf.Abs(d + line * 0.5f));
                px[y * n + x] = new Color32(255, 255, 255, (byte)(255f * a));
            }
        tex.SetPixels32(px); tex.Apply();
        frameSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(24, 24, 24, 24));
        return frameSprite;
    }

    static Sprite fillSprite;
    static Sprite FillSprite()
    {
        if (fillSprite != null) return fillSprite;
        const int n = 96; const float rad = 20f;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = Mathf.Abs(x + 0.5f - n / 2f) - (n / 2f - rad - 1f), dy = Mathf.Abs(y + 0.5f - n / 2f) - (n / 2f - rad - 1f);
                float d = Mathf.Sqrt(Mathf.Max(dx, 0f) * Mathf.Max(dx, 0f) + Mathf.Max(dy, 0f) * Mathf.Max(dy, 0f)) + Mathf.Min(Mathf.Max(dx, dy), 0f) - rad;
                px[y * n + x] = new Color32(255, 255, 255, (byte)(255f * Mathf.Clamp01(0.5f - d)));
            }
        tex.SetPixels32(px); tex.Apply();
        fillSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(24, 24, 24, 24));
        return fillSprite;
    }

    static Image Line(Transform parent, Vector2 pos, Vector2 size, Color col)
    {
        var im = new GameObject("Line", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        im.transform.SetParent(parent, false);
        var r = (RectTransform)im.transform; r.sizeDelta = size; r.anchoredPosition = pos;
        im.color = col; im.raycastTarget = false;
        return im;
    }

    // the filled rounded rectangle, as a fan from the centre (both windings), a little behind the outline
    static void RoundedFill(System.Collections.Generic.List<Vector3> v, System.Collections.Generic.List<int> t, Vector3 c, Vector3 r, Vector3 u, float w, float h, float rad, int seg = 6)
    {
        var pts = new System.Collections.Generic.List<Vector2>();
        float cx = w / 2 - rad, cy = h / 2 - rad;
        for (int corner = 0; corner < 4; corner++)
        {
            float sx = corner == 0 || corner == 3 ? 1f : -1f, sy = corner < 2 ? 1f : -1f, a0 = corner * 90f;
            for (int i = 0; i <= seg; i++) { float a = (a0 + i * (90f / seg)) * Mathf.Deg2Rad; pts.Add(new Vector2(sx * cx + Mathf.Cos(a) * rad, sy * cy + Mathf.Sin(a) * rad)); }
        }
        int k0 = v.Count; v.Add(c);
        foreach (var q in pts) v.Add(c + r * q.x + u * q.y);
        for (int i = 0; i < pts.Count; i++)
        {
            int a = k0 + 1 + i, b = k0 + 1 + (i + 1) % pts.Count;
            t.AddRange(new[] { k0, a, b, k0, b, a });
        }
    }

    // a rounded-rectangle outline as a strip of small bars (both windings), centred on c in the plane of r and u
    static void RoundedFrame(System.Collections.Generic.List<Vector3> v, System.Collections.Generic.List<int> t, Vector3 c, Vector3 r, Vector3 u, float w, float h, float bar, float rad, int seg = 6)
    {
        var pts = new System.Collections.Generic.List<Vector2>();
        float cx = w / 2 - rad, cy = h / 2 - rad;
        for (int corner = 0; corner < 4; corner++)                                           // top right, top left, bottom left, bottom right
        {
            float sx = corner == 0 || corner == 3 ? 1f : -1f, sy = corner < 2 ? 1f : -1f, a0 = corner * 90f;
            for (int i = 0; i <= seg; i++)
            {
                float a = (a0 + i * (90f / seg)) * Mathf.Deg2Rad;
                pts.Add(new Vector2(sx * cx + Mathf.Cos(a) * rad, sy * cy + Mathf.Sin(a) * rad));
            }
        }
        for (int i = 0; i < pts.Count; i++)
        {
            var a = pts[i]; var b = pts[(i + 1) % pts.Count];
            var d = (b - a); float len = d.magnitude; if (len < 1e-5f) continue; d /= len;
            var nrm2 = new Vector2(-d.y, d.x) * (bar * 0.5f);
            var o0 = c + r * a.x + u * a.y; var o1 = c + r * b.x + u * b.y; var off = r * nrm2.x + u * nrm2.y;
            int k = v.Count;
            v.Add(o0 - off); v.Add(o1 - off); v.Add(o1 + off); v.Add(o0 + off);
            t.AddRange(new[] { k, k + 1, k + 2, k, k + 2, k + 3, k, k + 2, k + 1, k, k + 3, k + 2 });
        }
    }

    // pops up (0.85 to 1 in size, fading in over a second) from `at` seconds after the step begins
    void Pop(Card c, float at)
    {
        if (c == null) return;
        float k = Mathf.Clamp01((t - at) / 1f);
        if (c.fig != null && c.fig.Skeleton != null)
        {
            var hips = c.fig.Bone(HumanBodyBones.Hips);
            if (!c.placed)
            {
                c.placed = true;
                var sk = c.fig.Skeleton;
                sk.localScale *= figureZoom;
                sk.RotateAround(hips.position, Vector3.up, Vector3.SignedAngle(Vector3.ProjectOnPlane(sk.forward, Vector3.up), c.face, Vector3.up));   // faces the player
                var want = new Vector3(c.pos.x, 0f, c.pos.z) + c.face * 0.04f;
                sk.position += new Vector3(want.x - hips.position.x, 0f, want.z - hips.position.z);
            }
            else if (!c.fitted)                                                             // next frame: scale so that the head stays inside the card
            {
                c.fitted = true;
                float avail = 0.36f, have = c.fig.HighestY - hips.position.y;               // hips-to-top of head that fits between the waist line and the card's top edge
                if (have > 0.05f) c.fig.Skeleton.localScale *= Mathf.Min(1f, avail / have);
            }
            else if (!c.corrected)                                                          // then: the hips go on the waist line, centred on the card
            {
                c.corrected = true;
                c.fig.groundY += c.waistY - hips.position.y;
                var sk = c.fig.Skeleton; var want = new Vector3(c.pos.x, 0f, c.pos.z) + c.face * 0.04f;
                sk.position += new Vector3(want.x - hips.position.x, 0f, want.z - hips.position.z);
            }
        }
        c.shown = k;
    }

    void Fade(Card c, float fade)
    {
        if (c == null) return;
        c.group.alpha = c.shown * fade;
        c.root.transform.localScale = Vector3.one * 0.001f * Mathf.Lerp(0.85f, 1f, c.shown);
        if (c.fig != null) c.fig.brightness = c.shown * fade * (c.corrected ? 1f : 0f);
    }

    // cards behind the two real ones, filling the dark all round and above and below eye level: a random story on each (a small canvas each), outlines (one mesh
    // per wave) and a waist-up particle person in its own colour (one particle system per wave). Golden-angle spacing keeps them from lining up; the radius
    // grows with the card number so the pile has depth, and farther cards are bigger.
    void BuildEndless(float cy)
    {
        var rnd = new System.Random(7);
        endless = new GameObject("EndlessCards"); mpb = new MaterialPropertyBlock(); mpb2 = new MaterialPropertyBlock();
        wave = new Wave[waves + Far.Length];
        for (int w = 0; w < waves; w++)
        {
            wave[w] = new Wave { at = endlessFrom + w * waveEvery };
            var cg = new GameObject("Stories", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            cg.transform.SetParent(endless.transform, false);
            cg.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            cg.transform.localScale = Vector3.one * 0.001f;
            wave[w].canvas = cg.transform; wave[w].texts = cg.GetComponent<CanvasGroup>(); wave[w].texts.alpha = 0f; wave[w].texts.blocksRaycasts = false;
        }
        // choose each card's size and distance, then a direction in which it does not sit in line with another card: the nearest (biggest) cards are placed first,
        // each new card must be clear of the others on screen (apparent size in degrees); when the sky fills up the rule is relaxed step by step, so the pile
        // gets denser but cards never line up exactly
        int n = cardCount;
        var rad = new float[n]; for (int i = 0; i < n; i++) rad[i] = 1.8f + 6f * Mathf.Pow((float)rnd.NextDouble(), 0.8f);
        System.Array.Sort(rad);
        var yawA = new float[n]; var pitA = new float[n]; var hwA = new float[n]; var hhA = new float[n];
        float relax = 1.1f;
        for (int i = 0; i < n; i++)
        {
            float r = rad[i], size = Mathf.Lerp(0.55f, 1.7f, (r - 1.8f) / 6f);
            float hw = Mathf.Atan(0.25f * size / r) * Mathf.Rad2Deg, hh = Mathf.Atan(0.38f * size / r) * Mathf.Rad2Deg;
            float bestY = 0f, bestP = 0f, bestScore = -1f;
            for (int tries = 0; tries < 60; tries++)
            {
                float yaw = (float)rnd.NextDouble() * 360f;
                float pit = Mathf.Clamp(10f + 28f * (float)(rnd.NextDouble() + rnd.NextDouble() + rnd.NextDouble() - 1.5) * 1.6f, -40f, 65f);
                float score = 99f;                                                          // the smallest separation to another card, in units of the two cards' sizes
                for (int j = 0; j < i; j++)
                {
                    float dy = Mathf.Abs(Mathf.DeltaAngle(yaw, yawA[j])) / ((hw + hwA[j]) * 1f), dp = Mathf.Abs(pit - pitA[j]) / (hh + hhA[j]);
                    score = Mathf.Min(score, Mathf.Max(dy, dp));
                }
                if (score > bestScore) { bestScore = score; bestY = yaw; bestP = pit; }
                if (score >= relax) break;
            }
            if (bestScore < relax) relax = Mathf.Max(0.25f, relax * 0.97f);                  // too crowded: allow a little more overlap from now on
            yawA[i] = bestY; pitA[i] = bestP; hwA[i] = hw; hhA[i] = hh;
        }
        for (int i = 0; i < n; i++)
        {
            var wv = wave[rnd.Next(waves)];
            float r = rad[i], size = Mathf.Lerp(0.55f, 1.7f, (r - 1.8f) / 6f);
            var flat = Quaternion.AngleAxis(yawA[i], Vector3.up) * fwd;
            float pr = pitA[i] * Mathf.Deg2Rad;
            var p = head.position + (flat * Mathf.Cos(pr) + Vector3.up * Mathf.Sin(pr)) * r;
            wv.cpos.Add(p);
            var nrm = (head.position - p).normalized;                                       // the card tilts to face the head
            var right = Vector3.Cross(Vector3.up, -nrm).normalized; var up = Vector3.Cross(-nrm, right);
            float roll = (float)(rnd.NextDouble() - 0.5) * 10f * Mathf.Deg2Rad, cr = Mathf.Cos(roll), sr = Mathf.Sin(roll);       // each card sits a little crooked
            var right2 = right * cr + up * sr; up = up * cr - right * sr; right = right2;
            float w2 = 0.5f * size, h = 0.76f * size, bar = 0.006f * size;
            RoundedFrame(wv.verts, wv.tris, p, right, up, w2, h, bar, 0.02f * size);
            RoundedFill(wv.fverts, wv.ftris, p - nrm * 0.003f, right, up, w2, h, 0.02f * size);                                   // the glass, just behind the outline
            var col = Palette[rnd.Next(Palette.Length)];
            for (int j = 0; j < 16; j++) { float a2 = (float)(rnd.NextDouble() * Mathf.PI * 2), rr = 0.06f * size * Mathf.Sqrt((float)rnd.NextDouble()); wv.pp.Add(p + right * Mathf.Cos(a2) * rr + up * (0.2f * size + Mathf.Sin(a2) * rr) + nrm * 0.004f); wv.pc.Add(col); }
            for (int j = 0; j < 56; j++) { float a2 = (float)(rnd.NextDouble() * Mathf.PI * 2), rr = Mathf.Sqrt((float)rnd.NextDouble()); wv.pp.Add(p + right * Mathf.Cos(a2) * rr * 0.19f * size + up * (Mathf.Sin(a2) * rr * 0.15f * size) + nrm * 0.004f); wv.pc.Add(col); }
            MakeStoryText(wv, p, nrm, up, size, NextName(), NextLong());
        }
        for (int w = 0; w < waves; w++) Finish(wave[w], rnd);
        // beyond the cards that can be read: bands of cards receding into the dark, each dimmer and farther than the last (see Far); each band is built a moment
        // before it appears so that the work is spread out
        eye0 = head.position;
        for (int bnd = 0; bnd < Far.Length; bnd++) wave[waves + bnd] = new Wave { at = endlessFrom + 4f + bnd * 5f, dim = Far[bnd].dim, band = bnd, built = false };
    }

    // one far band: smaller cards (they stay close to the size of the readable ones, so they shrink with distance), an avatar of a few big particles in each, a
    // name and a short line of real words (just the name in the farthest bands); Halton points spread them over the sky without lines
    void BuildBand(Wave wv)
    {
        var fb = Far[wv.band]; var rnd = new System.Random(100 + wv.band);
        var cg = new GameObject("Stories", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
        cg.transform.SetParent(endless.transform, false);
        cg.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        cg.transform.localScale = Vector3.one * 0.001f;
        wv.canvas = cg.transform; wv.texts = cg.GetComponent<CanvasGroup>(); wv.texts.alpha = 0f; wv.texts.blocksRaycasts = false;
        int hal = 1; for (int b2 = 0; b2 < wv.band; b2++) hal += Far[b2].count;
        for (int i = 0; i < fb.count; i++, hal++)
        {
            float yaw = Halton(hal, 2) * 360f, pit = Mathf.Lerp(-35f, 62f, Halton(hal, 3));
            float r = Mathf.Lerp(fb.r0, fb.r1, (float)rnd.NextDouble()), size = 1.7f + 0.012f * (r - 8f);
            var flat = Quaternion.AngleAxis(yaw, Vector3.up) * fwd; float pr = pit * Mathf.Deg2Rad;
            var p = eye0 + (flat * Mathf.Cos(pr) + Vector3.up * Mathf.Sin(pr)) * r;
            var nrm = (eye0 - p).normalized;
            var right = Vector3.Cross(Vector3.up, -nrm).normalized; var up = Vector3.Cross(-nrm, right);
            float roll = (float)(rnd.NextDouble() - 0.5) * 10f * Mathf.Deg2Rad, cr = Mathf.Cos(roll), sr = Mathf.Sin(roll);
            var right2 = right * cr + up * sr; up = up * cr - right * sr; right = right2;
            float w2 = 0.5f * size, h = 0.76f * size, bar = 0.006f * size;
            RoundedFrame(wv.verts, wv.tris, p, right, up, w2, h, bar, 0.02f * size, 2);
            if (fb.glass) RoundedFill(wv.fverts, wv.ftris, p - nrm * 0.003f * size, right, up, w2, h, 0.02f * size, 2);
            var col = Palette[rnd.Next(Palette.Length)];
            for (int j = 0; j < fb.people; j++)
            {
                float a2 = (float)(rnd.NextDouble() * Mathf.PI * 2), rr = Mathf.Sqrt((float)rnd.NextDouble());
                bool headPart = j < Mathf.Max(1, fb.people / 3);
                wv.pp.Add(headPart ? p + right * Mathf.Cos(a2) * rr * 0.04f * size + up * (0.2f * size + Mathf.Sin(a2) * rr * 0.04f * size) + nrm * 0.004f * size
                                   : p + right * Mathf.Cos(a2) * rr * 0.16f * size + up * (Mathf.Sin(a2) * rr * 0.10f * size) + nrm * 0.004f * size);
                wv.pc.Add(col); wv.psz.Add((headPart ? 0.020f : 0.026f) * size * (0.8f + 0.4f * (float)rnd.NextDouble()));
            }
            MakeStoryText(wv, p, nrm, up, size, NextName(), fb.chars <= 0 ? "" : fb.r0 < 20f ? NextLong() : NextShort());   // the two nearest far bands read as full stories, the next two as a short line; the farthest two show only a name
        }
        Finish(wv, rnd);
        wv.built = true;
    }

    static string Short(string s, int n)
    {
        if (n <= 0) return "";
        if (s.Length <= n) return s;
        int i = s.LastIndexOf(' ', n); if (i < 10) i = n;
        return s.Substring(0, i) + "...";
    }

    // the meshes, glass and particles of one wave
    void Finish(Wave wv, System.Random rnd)
    {
            var go = new GameObject("Wave"); go.transform.SetParent(endless.transform, false);
            var mesh = new Mesh { name = "CardFrames", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 }; mesh.SetVertices(wv.verts); mesh.SetTriangles(wv.tris, 0); mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            if (wv.fverts.Count > 0)
            {
                var fgo = new GameObject("Glass"); fgo.transform.SetParent(go.transform, false);
                var fmesh = new Mesh { name = "CardGlass", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 }; fmesh.SetVertices(wv.fverts); fmesh.SetTriangles(wv.ftris, 0); fmesh.RecalculateBounds();
                fgo.AddComponent<MeshFilter>().sharedMesh = fmesh;
                wv.fills = fgo.AddComponent<MeshRenderer>(); wv.fills.sharedMaterial = GlassMaterial();
                wv.fills.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; wv.fills.receiveShadows = false; wv.fills.enabled = false;
            }
            wv.frames = go.AddComponent<MeshRenderer>(); wv.frames.sharedMaterial = Resources.Load<Material>("OrangeSphere");
            wv.frames.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; wv.frames.receiveShadows = false; wv.frames.enabled = false;
            if (wv.pp.Count > 0)
            {
                var pgo = new GameObject("People"); pgo.transform.SetParent(go.transform, false);
                var ps = pgo.AddComponent<ParticleSystem>();
                var main = ps.main; main.simulationSpace = ParticleSystemSimulationSpace.World; main.playOnAwake = false; main.maxParticles = wv.pp.Count;
                var em = ps.emission; em.enabled = false; var sh = ps.shape; sh.enabled = false;
                wv.people = pgo.GetComponent<ParticleSystemRenderer>(); wv.people.sharedMaterial = Resources.Load<Material>("ChainParticle");
                wv.mat = wv.people.material; wv.mat.SetTexture("_BaseMap", ParticleFigure.DiscTexture());
                var parts = new ParticleSystem.Particle[wv.pp.Count];
                for (int i = 0; i < parts.Length; i++) { parts[i].position = wv.pp[i]; parts[i].startSize = wv.psz.Count == parts.Length ? wv.psz[i] : 0.008f * (0.8f + 0.4f * (float)rnd.NextDouble()); parts[i].startLifetime = 1000f; parts[i].remainingLifetime = 1000f; var c = wv.pc[i]; c.a = 1f; parts[i].startColor = c; }
                ps.SetParticles(parts, parts.Length); ps.Play(); wv.people.enabled = false;
            }
    }

    // the story under the portrait: a text child on the wave's one canvas (canvas units are 1 mm), turned to face the player
    void MakeStoryText(Wave wv, Vector3 p, Vector3 nrm, Vector3 up, float size, string name, string story)
    {
        var tx = new GameObject("Text", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
        tx.transform.SetParent(wv.canvas, false);
        var r = (RectTransform)tx.transform; r.sizeDelta = new Vector2(440f, 190f);
        r.localPosition = (p - up * (0.283f * size) + nrm * 0.004f) * 1000f; r.localRotation = Quaternion.LookRotation(-nrm, up); r.localScale = Vector3.one * size;
        tx.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); tx.fontSize = story.Length > 120 ? 24 : story.Length > 90 ? 28 : 32; tx.alignment = TextAnchor.MiddleCenter; tx.supportRichText = true;   // the stories now have two parts: a long one gets a smaller size so it stays on the card
        tx.horizontalOverflow = HorizontalWrapMode.Wrap; tx.verticalOverflow = VerticalWrapMode.Overflow; tx.raycastTarget = false;
        tx.text = "<b>" + name.ToUpper() + "</b>\n" + story; tx.color = Color.white;
    }

    // a flat bar facing the player, written with both windings so it shows from either side whatever the material culls
    static void Quad(System.Collections.Generic.List<Vector3> v, System.Collections.Generic.List<int> t, Vector3 c, Vector3 r, Vector3 u, Vector2 at, Vector2 size)
    {
        int i = v.Count; var o = c + r * at.x + u * at.y; var hr = r * size.x * 0.5f; var hu = u * size.y * 0.5f;
        v.Add(o - hr - hu); v.Add(o + hr - hu); v.Add(o + hr + hu); v.Add(o - hr + hu);
        t.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3, i, i + 2, i + 1, i, i + 3, i + 2 });
    }

    void BuildChainsLine()
    {
        chainsGo = new GameObject("ChainsLine", typeof(RectTransform), typeof(Canvas));
        chainsGo.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        ((RectTransform)chainsGo.transform).sizeDelta = new Vector2(1100f, 200f);
        chainsGo.transform.localScale = Vector3.one * 0.001f;
        var p = origin + fwd * 1.2f; p.y = head.position.y - 0.05f;
        chainsGo.transform.position = p; chainsGo.transform.rotation = Quaternion.LookRotation(p - head.position);
        chainsText = new GameObject("Text", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
        chainsText.transform.SetParent(chainsGo.transform, false);
        var r = (RectTransform)chainsText.transform; r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
        chainsText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); chainsText.fontSize = 56; chainsText.fontStyle = FontStyle.Bold;
        chainsText.alignment = TextAnchor.MiddleCenter; chainsText.horizontalOverflow = HorizontalWrapMode.Wrap; chainsText.raycastTarget = false;
        chainsText.text = "WERE THE CHAINS\nREALLY BINDING YOU?"; chainsText.color = new Color(1f, 1f, 1f, 0f);
    }

    void End()
    {
        StopMusic();
        foreach (var c in new[] { stephen, julia })
            if (c != null) { if (c.fig != null) Destroy(c.fig.gameObject); Destroy(c.root); }
        if (endless != null) Destroy(endless);
        if (wave != null) foreach (var w in wave) if (w.mat != null) Destroy(w.mat);
        if (chainsGo != null) Destroy(chainsGo);
        wave = null;
        stephen = julia = null; endless = null; chainsGo = null; phase = Phase.Idle; t = 0f;
        Log("scene 4 cards end");
    }

    static void Log(string msg) => Debug.Log($"BYSTANDER EVENT t={Time.time:0.00} cards {msg}");
}
