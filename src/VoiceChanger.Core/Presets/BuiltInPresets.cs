using VoiceChanger.Core.Presets;

namespace VoiceChanger.Core.Presets;

/// <summary>Factory for the read-only built-in preset library.</summary>
public static class BuiltInPresets
{
    private static readonly DateTime Stamp = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public static IReadOnlyList<VoicePreset> All { get; } = Create();

    private static VoicePreset P(string id, string name, string category, string icon, string description, string[] tags, Action<VoiceProfile> configure)
    {
        var profile = new VoiceProfile();
        configure(profile);
        return new VoicePreset
        {
            Id = id, Name = name, Category = category, Icon = icon, Description = description, Tags = tags,
            IsBuiltIn = true, CreatedUtc = Stamp, ModifiedUtc = Stamp, Profile = profile
        };
    }

    private static List<VoicePreset> Create() => new()
    {
        // ── Female Voices ──
        P("builtin-soft-warm-woman", "Soft & Warm Woman", PresetCategories.Female, "🌷", "A gentle, warm feminine tone with light breath for an approachable, soothing sound.", new[] { "female", "soft", "warm", "gentle" }, v =>
        {
            v.PitchSemitones = 5.5f; v.FormantSemitones = 2.3f; v.Breathiness = 0.15f;
            v.HighPassHz = 120; v.LowShelfDb = -2f; v.HighShelfDb = 1.5f;
            v.DeEsserEnabled = true; v.DeEsserAmount = 0.4f;
        }),
        P("builtin-bright-energetic-woman", "Bright & Energetic Woman", PresetCategories.Female, "✨", "A lively, bright feminine tone with lifted top end for upbeat, energetic content.", new[] { "female", "bright", "energetic", "upbeat" }, v =>
        {
            v.PitchSemitones = 6.5f; v.FormantSemitones = 2.8f; v.Breathiness = 0.1f;
            v.HighPassHz = 130; v.HighShelfDb = 2.2f; v.Peak3Db = 1.5f;
            v.DeEsserEnabled = true; v.DeEsserAmount = 0.4f;
        }),
        P("builtin-deep-sultry-husky", "Deep Sultry Husky Woman", PresetCategories.Female, "🥂", "A low, husky feminine tone with a touch of rasp for a sultry, smoky sound.", new[] { "female", "sultry", "husky", "low", "smoky" }, v =>
        {
            v.PitchSemitones = 5f; v.FormantSemitones = 2.2f; v.Rasp = 0.18f; v.Breathiness = 0.2f;
            v.HighPassHz = 115; v.LowShelfDb = -2f;
            v.DeEsserEnabled = true; v.DeEsserAmount = 0.4f;
        }),
        P("builtin-girl-next-door", "Girl Next Door", PresetCategories.Female, "🌼", "A friendly, natural feminine voice with an easygoing, relatable character.", new[] { "female", "natural", "friendly", "casual" }, v =>
        {
            v.PitchSemitones = 6f; v.FormantSemitones = 2.5f; v.Breathiness = 0.12f; v.Nasality = 0.05f;
            v.HighPassHz = 125; v.LowShelfDb = -2.5f; v.HighShelfDb = 1.5f;
            v.DeEsserEnabled = true; v.DeEsserAmount = 0.4f;
        }),
        P("builtin-corporate-professional-woman", "Corporate Professional Woman", PresetCategories.Female, "💼", "A crisp, polished feminine voice tuned for business presentations and calls.", new[] { "female", "corporate", "professional", "polished" }, v =>
        {
            v.PitchSemitones = 5f; v.FormantSemitones = 2.2f; v.Breathiness = 0.1f;
            v.HighPassHz = 130; v.Peak3Hz = 3200; v.Peak3Db = 1.5f; v.HighShelfDb = 1.5f;
            v.DeEsserEnabled = true; v.DeEsserAmount = 0.4f;
            v.CompRatio = 3.5f; v.CompThresholdDb = -19f; v.CompMakeupDb = 3f;
        }),
        P("builtin-southern-warm-woman", "Southern Warm Woman", PresetCategories.Female, "🌻", "A slow, warm feminine tone with rounded low-mids for an inviting Southern-style warmth.", new[] { "female", "southern", "warm", "welcoming" }, v =>
        {
            v.PitchSemitones = 5.5f; v.FormantSemitones = 2.4f; v.Breathiness = 0.15f;
            v.HighPassHz = 120; v.Peak1Hz = 320; v.Peak1Db = 1f; v.LowShelfDb = -2f;
            v.DeEsserEnabled = true; v.DeEsserAmount = 0.4f;
        }),
        P("builtin-late-night-radio-host-woman", "Late-Night Radio Host (Female)", PresetCategories.Female, "🌙", "A velvety, relaxed feminine radio-host tone for after-hours broadcasts.", new[] { "female", "radio", "late-night", "smooth" }, v =>
        {
            v.PitchSemitones = 5f; v.FormantSemitones = 2.2f; v.Breathiness = 0.15f;
            v.HighPassHz = 115; v.LowShelfDb = -3f; v.HighShelfDb = 1f;
            v.DeEsserEnabled = true; v.DeEsserAmount = 0.4f;
            v.CompRatio = 3f; v.CompMakeupDb = 3f;
        }),
        P("builtin-whispery-intimate", "Whispery Intimate", PresetCategories.Female, "🤍", "A soft, close-mic feminine whisper for intimate narration and ASMR-style content.", new[] { "female", "whisper", "intimate", "soft", "asmr" }, v =>
        {
            v.PitchSemitones = 5.5f; v.FormantSemitones = 2.4f; v.Breathiness = 0.3f; v.InputGainDb = 3f;
            v.HighPassHz = 110;
            v.DeEsserEnabled = true; v.DeEsserAmount = 0.4f;
            v.CompRatio = 4f; v.CompThresholdDb = -22f; v.CompMakeupDb = 3f;
        }),
        P("builtin-confident-presenter-woman", "Confident Presenter Woman", PresetCategories.Female, "🎤", "A forward, assured feminine tone with extra presence for confident public speaking.", new[] { "female", "confident", "presenter", "presence" }, v =>
        {
            v.PitchSemitones = 5.5f; v.FormantSemitones = 2.5f; v.Breathiness = 0.1f;
            v.HighPassHz = 125; v.Peak3Hz = 3200; v.Peak3Db = 2f; v.HighShelfDb = 1.5f;
            v.DeEsserEnabled = true; v.DeEsserAmount = 0.4f;
            v.CompRatio = 3f; v.CompMakeupDb = 3f;
        }),
        P("builtin-gentle-storyteller", "Gentle Storyteller", PresetCategories.Female, "📖", "A warm, unhurried feminine narration voice suited to bedtime stories and audiobooks.", new[] { "female", "storyteller", "gentle", "narration" }, v =>
        {
            v.PitchSemitones = 5.5f; v.FormantSemitones = 2.4f; v.Breathiness = 0.18f;
            v.HighPassHz = 115; v.LowShelfDb = -2f; v.HighShelfDb = 1f;
            v.DeEsserEnabled = true; v.DeEsserAmount = 0.4f;
        }),
        P("builtin-playful-upbeat-woman", "Playful Upbeat Woman", PresetCategories.Female, "🎈", "A bouncy, bright feminine tone full of playful energy for casual content.", new[] { "female", "playful", "upbeat", "bright" }, v =>
        {
            v.PitchSemitones = 6.5f; v.FormantSemitones = 3f; v.Breathiness = 0.1f;
            v.HighPassHz = 130; v.HighShelfDb = 2f;
            v.DeEsserEnabled = true; v.DeEsserAmount = 0.4f;
        }),
        P("builtin-calm-yoga-instructor", "Calm Yoga Instructor", PresetCategories.Female, "🧘‍♀️", "A slow, breathy, soothing feminine voice for meditation and yoga guidance.", new[] { "female", "calm", "yoga", "soothing", "meditation" }, v =>
        {
            v.PitchSemitones = 5f; v.FormantSemitones = 2.2f; v.Breathiness = 0.25f;
            v.HighPassHz = 115; v.LowShelfDb = -2f;
            v.DeEsserEnabled = true; v.DeEsserAmount = 0.4f;
        }),
        P("builtin-sassy-influencer", "Sassy Influencer", PresetCategories.Female, "💅", "A confident feminine tone with a light vocal fry for a sassy, social-media-ready sound.", new[] { "female", "sassy", "influencer", "vocal-fry" }, v =>
        {
            v.PitchSemitones = 6.5f; v.FormantSemitones = 3f; v.Rasp = 0.18f; v.Breathiness = 0.1f;
            v.HighPassHz = 130; v.HighShelfDb = 1.5f;
            v.DeEsserEnabled = true; v.DeEsserAmount = 0.4f;
        }),
        P("builtin-mom-voice", "Mom Voice", PresetCategories.Female, "❤️", "A warm, caring feminine tone with a faint natural tremor for a comforting, motherly sound.", new[] { "female", "mom", "warm", "caring" }, v =>
        {
            v.PitchSemitones = 5.5f; v.FormantSemitones = 2.3f; v.Breathiness = 0.14f;
            v.TremorRateHz = 5.5f; v.TremorPitchDepth = 0.05f; v.TremorAmpDepth = 0.04f;
            v.HighPassHz = 120; v.Peak1Hz = 320; v.Peak1Db = 1f; v.LowShelfDb = -2f;
            v.DeEsserEnabled = true; v.DeEsserAmount = 0.4f;
        }),
        P("builtin-grandmother-70", "Grandmother (70+)", PresetCategories.Female, "👵", "A frail, breathy elderly feminine voice with a gentle vocal tremor for grandmotherly warmth.", new[] { "female", "elderly", "grandmother", "tremor", "breathy" }, v =>
        {
            v.PitchSemitones = 4.5f; v.FormantSemitones = 2f; v.Breathiness = 0.3f;
            v.TremorRateHz = 6f; v.TremorPitchDepth = 0.2f; v.TremorAmpDepth = 0.18f;
            v.HighPassHz = 130; v.HighShelfDb = -4f;
            v.DeEsserEnabled = true; v.DeEsserAmount = 0.4f;
        }),
        P("builtin-teen-girl-16", "Teen Girl (16)", PresetCategories.Female, "🎀", "A bright teenage feminine voice with a light nasal edge, great for younger character work.", new[] { "female", "teen", "young", "bright" }, v =>
        {
            v.PitchSemitones = 7f; v.FormantSemitones = 3.3f; v.Nasality = 0.1f; v.Breathiness = 0.12f;
            v.HighPassHz = 135; v.HighShelfDb = 2f;
            v.DeEsserEnabled = true; v.DeEsserAmount = 0.4f;
        }),
        P("builtin-young-woman-22", "Young Woman (22)", PresetCategories.Female, "🌸", "A clear, bright young-adult feminine voice for everyday chat and content creation.", new[] { "female", "young-adult", "clear", "bright" }, v =>
        {
            v.PitchSemitones = 6f; v.FormantSemitones = 2.8f; v.Breathiness = 0.12f;
            v.HighPassHz = 130; v.HighShelfDb = 1.5f;
            v.DeEsserEnabled = true; v.DeEsserAmount = 0.4f;
        }),
        P("builtin-mature-woman-45", "Mature Woman (45)", PresetCategories.Female, "🍷", "A grounded, confident feminine voice with softened highs for a mature, composed tone.", new[] { "female", "mature", "grounded", "confident" }, v =>
        {
            v.PitchSemitones = 5f; v.FormantSemitones = 2.2f; v.Breathiness = 0.1f;
            v.HighPassHz = 120; v.LowShelfDb = -2f; v.HighShelfDb = 1f;
            v.DeEsserEnabled = true; v.DeEsserAmount = 0.4f;
        }),
        P("builtin-studio-singer", "Studio Singer", PresetCategories.Female, "🎙️", "A breathy, presence-forward feminine voice tuned like a close-mic'd studio vocal take.", new[] { "female", "singer", "studio", "presence" }, v =>
        {
            v.PitchSemitones = 5.5f; v.FormantSemitones = 2.5f; v.Breathiness = 0.12f;
            v.HighPassHz = 115; v.Peak3Hz = 3200; v.Peak3Db = 2f; v.HighShelfDb = 1.5f;
            v.DeEsserEnabled = true; v.DeEsserAmount = 0.4f;
        }),
        P("builtin-nurse-caring", "Nurse / Caring Voice", PresetCategories.Female, "🩺", "A gentle, reassuring feminine voice suited to caregiving and patient-facing roles.", new[] { "female", "nurse", "caring", "gentle", "reassuring" }, v =>
        {
            v.PitchSemitones = 5.5f; v.FormantSemitones = 2.4f; v.Breathiness = 0.15f;
            v.HighPassHz = 120; v.LowShelfDb = -2f; v.HighShelfDb = 1f;
            v.DeEsserEnabled = true; v.DeEsserAmount = 0.4f;
        }),

        // ── Male Voices ──
        P("builtin-deep-baritone", "Deep Baritone", PresetCategories.Male, "🎙️", "A rich, resonant baritone voice with boosted low end for a commanding sound.", new[] { "male", "baritone", "deep", "resonant" }, v =>
        {
            v.PitchSemitones = -3.5f; v.FormantSemitones = -1.8f; v.LowShelfDb = 3f;
        }),
        P("builtin-smooth-bass-announcer", "Smooth Bass Announcer", PresetCategories.Male, "📢", "A deep, smooth bass voice tuned like a classic broadcast announcer.", new[] { "male", "bass", "announcer", "smooth", "deep" }, v =>
        {
            v.PitchSemitones = -4.5f; v.FormantSemitones = -2.2f; v.LowShelfDb = 3.5f;
        }),
        P("builtin-warm-tenor", "Warm Tenor", PresetCategories.Male, "🎵", "A light, warm tenor voice with a touch of low-mid fullness.", new[] { "male", "tenor", "warm", "light" }, v =>
        {
            v.PitchSemitones = 1f; v.FormantSemitones = 0.3f; v.LowShelfDb = 1f;
        }),
        P("builtin-raspy-rocker", "Raspy Rocker", PresetCategories.Male, "🎸", "A gritty, textured masculine voice with rock-vocal rasp.", new[] { "male", "raspy", "rocker", "gritty" }, v =>
        {
            v.PitchSemitones = -1f; v.FormantSemitones = -0.3f; v.Rasp = 0.28f;
        }),
        P("builtin-gentle-soft-spoken", "Gentle Soft-Spoken", PresetCategories.Male, "🕊️", "A calm, breathy masculine voice with softened highs for a gentle, low-key tone.", new[] { "male", "gentle", "soft-spoken", "calm" }, v =>
        {
            v.PitchSemitones = -0.5f; v.FormantSemitones = -0.2f; v.Breathiness = 0.15f; v.HighShelfDb = -1f;
        }),
        P("builtin-laid-back-surfer", "Laid-Back Surfer", PresetCategories.Male, "🏄", "A relaxed, easygoing masculine voice with a hint of nasal drawl.", new[] { "male", "laid-back", "surfer", "relaxed" }, v =>
        {
            v.PitchSemitones = -1f; v.FormantSemitones = -0.3f; v.Nasality = 0.08f;
            v.TremorRateHz = 4f; v.TremorPitchDepth = 0.05f; v.TremorAmpDepth = 0.04f;
        }),
        P("builtin-executive-boss", "Executive Boss", PresetCategories.Male, "🧑‍💼", "An assured, forward masculine voice with extra presence for decisive leadership tone.", new[] { "male", "executive", "boss", "confident", "presence" }, v =>
        {
            v.PitchSemitones = -2f; v.FormantSemitones = -1f; v.Peak3Hz = 3200; v.Peak3Db = 2.5f;
            v.CompRatio = 4f; v.CompThresholdDb = -18f; v.CompMakeupDb = 3.5f;
        }),
        P("builtin-drill-sergeant", "Drill Sergeant", PresetCategories.Male, "🪖", "A hard-edged, gritty masculine voice with a barked midrange punch.", new[] { "male", "drill-sergeant", "gritty", "punchy" }, v =>
        {
            v.PitchSemitones = -1.5f; v.FormantSemitones = -0.5f; v.Rasp = 0.2f; v.Peak2Hz = 2000; v.Peak2Db = 3f;
            v.CompRatio = 4f; v.CompThresholdDb = -18f; v.CompMakeupDb = 4f;
        }),
        P("builtin-gamer-bro", "Gamer Bro", PresetCategories.Male, "🎮", "An energetic, forward masculine voice tuned to cut through game audio and chat.", new[] { "male", "gamer", "energetic", "presence" }, v =>
        {
            v.PitchSemitones = 0.5f; v.FormantSemitones = 0.3f; v.Peak3Hz = 3200; v.Peak3Db = 2f;
            v.CompRatio = 3.5f; v.CompMakeupDb = 3f;
        }),
        P("builtin-nerdy-guy", "Nerdy Guy", PresetCategories.Male, "🤓", "A slightly nasal, higher-pitched masculine voice for an earnest, bookish character.", new[] { "male", "nerdy", "nasal", "earnest" }, v =>
        {
            v.PitchSemitones = 1.5f; v.FormantSemitones = 1f; v.Nasality = 0.18f;
        }),
        P("builtin-southern-gentleman", "Southern Gentleman", PresetCategories.Male, "🤠", "A warm, unhurried masculine voice with rounded low-mids for a courteous Southern tone.", new[] { "male", "southern", "gentleman", "warm" }, v =>
        {
            v.PitchSemitones = -1.5f; v.FormantSemitones = -0.5f; v.Peak1Hz = 350; v.Peak1Db = 1.5f; v.LowShelfDb = 1.5f;
        }),
        P("builtin-grandfather-75", "Grandfather (75+)", PresetCategories.Male, "👴", "A frail, gravelly elderly masculine voice with a natural vocal tremor and softened top end.", new[] { "male", "elderly", "grandfather", "tremor", "raspy" }, v =>
        {
            v.PitchSemitones = -1.5f; v.FormantSemitones = -0.8f;
            v.TremorRateHz = 5.8f; v.TremorPitchDepth = 0.25f; v.TremorAmpDepth = 0.2f;
            v.Breathiness = 0.3f; v.Rasp = 0.15f; v.HighShelfDb = -4f; v.LowPassHz = 9000;
        }),
        P("builtin-teen-boy-15", "Teen Boy (15)", PresetCategories.Male, "🎤", "A lighter, slightly breathy teenage masculine voice with a hint of voice-break character.", new[] { "male", "teen", "young", "breathy" }, v =>
        {
            v.PitchSemitones = 2.5f; v.FormantSemitones = 1.2f; v.Breathiness = 0.1f;
        }),
        P("builtin-college-guy", "College Guy", PresetCategories.Male, "🎓", "A light, casual young-adult masculine voice for everyday conversation.", new[] { "male", "college", "young-adult", "casual" }, v =>
        {
            v.PitchSemitones = 0.5f; v.FormantSemitones = 0.5f;
        }),
        P("builtin-big-heavyweight", "Big Heavyweight", PresetCategories.Male, "🏋️", "A big, chesty masculine voice with heavy low-end weight and a touch of gravel.", new[] { "male", "big", "heavyweight", "deep", "powerful" }, v =>
        {
            v.PitchSemitones = -4f; v.FormantSemitones = -2.4f; v.LowShelfDb = 4f; v.Rasp = 0.1f;
            v.CompRatio = 3.5f;
        }),
        P("builtin-skinny-nervous-guy", "Skinny Nervous Guy", PresetCategories.Male, "😰", "A thin, higher masculine voice with a light nervous tremor for an anxious character.", new[] { "male", "skinny", "nervous", "tremor" }, v =>
        {
            v.PitchSemitones = 2f; v.FormantSemitones = 1.5f;
            v.TremorRateHz = 6.5f; v.TremorPitchDepth = 0.08f; v.TremorAmpDepth = 0.06f;
        }),
        P("builtin-movie-trailer-narrator", "Movie Trailer Narrator", PresetCategories.Male, "🎬", "An ultra-deep, booming cinematic narration voice inspired by classic movie trailers.", new[] { "male", "trailer", "narrator", "deep", "cinematic" }, v =>
        {
            v.PitchSemitones = -3f; v.FormantSemitones = -1.5f; v.LowShelfDb = 4f; v.ReverbMix = 0.06f;
            v.CompRatio = 4f; v.CompThresholdDb = -18f; v.CompMakeupDb = 4f;
        }),
        P("builtin-late-night-dj", "Late-Night DJ", PresetCategories.Male, "📻", "A smooth, warm masculine radio-DJ voice with boosted lows for after-hours broadcasts.", new[] { "male", "radio", "dj", "late-night", "smooth" }, v =>
        {
            v.PitchSemitones = -2f; v.FormantSemitones = -0.8f; v.LowShelfDb = 3f;
            v.CompRatio = 4f; v.CompThresholdDb = -18f; v.CompMakeupDb = 4f;
        }),
        P("builtin-everyday-podcast-voice", "Everyday Podcast Voice", PresetCategories.Male, "🎙️", "A neutral, clear masculine voice with light clarity EQ for casual podcasting.", new[] { "male", "podcast", "neutral", "clarity" }, v =>
        {
            v.HighPassHz = 100; v.Peak1Hz = 250; v.Peak1Db = -1.5f; v.Peak3Hz = 3000; v.Peak3Db = 2f; v.HighShelfDb = 1f;
        }),
        P("builtin-sports-commentator", "Sports Commentator", PresetCategories.Male, "🏆", "An energetic, forward masculine voice built to carry over crowd noise and excitement.", new[] { "male", "sports", "commentator", "energetic", "presence" }, v =>
        {
            v.PitchSemitones = -0.5f; v.FormantSemitones = -0.3f; v.Peak3Hz = 3200; v.Peak3Db = 3f;
            v.CompRatio = 4f; v.CompThresholdDb = -18f; v.CompMakeupDb = 4f;
        }),

        // ── Age ──
        P("builtin-kid-8-10", "Kid (Age 8-10)", PresetCategories.Age, "🧒", "A bright, gender-neutral child's voice for playful chat, streaming, and story-time.", new[] { "age", "kid", "child", "neutral", "playful" }, v =>
        {
            v.PitchSemitones = 7.5f; v.FormantSemitones = 3.8f; v.Breathiness = 0.15f;
            v.HighPassHz = 160; v.LowShelfDb = -4f; v.Peak3Hz = 3800; v.Peak3Db = 2f;
        }),
        P("builtin-kid-5-6", "Kid (Age 5-6)", PresetCategories.Age, "🍼", "A tiny, breathy young-child voice for playful skits and lighthearted content.", new[] { "age", "kid", "toddler", "high-pitch", "playful" }, v =>
        {
            v.PitchSemitones = 8f; v.FormantSemitones = 4f; v.Breathiness = 0.22f;
            v.HighPassHz = 170; v.LowShelfDb = -4f;
        }),
        P("builtin-tween-11-13", "Tween (Age 11-13)", PresetCategories.Age, "🎈", "A gender-neutral preteen voice, a step above kid pitch, good for younger-sounding characters.", new[] { "age", "tween", "preteen", "neutral" }, v =>
        {
            v.PitchSemitones = 4.5f; v.FormantSemitones = 2.2f; v.Breathiness = 0.12f; v.HighPassHz = 140;
        }),
        P("builtin-teen-boy-14", "Teen Boy (14)", PresetCategories.Age, "🎤", "A light, slightly breathy 14-year-old masculine voice for younger character work.", new[] { "age", "teen", "boy", "young-male" }, v =>
        {
            v.PitchSemitones = 2f; v.FormantSemitones = 1f; v.Breathiness = 0.12f;
        }),
        P("builtin-teen-girl-15", "Teen Girl (15)", PresetCategories.Age, "🎀", "A bright 15-year-old feminine voice with light nasality for younger character work.", new[] { "age", "teen", "girl", "young-female" }, v =>
        {
            v.PitchSemitones = 7f; v.FormantSemitones = 3.2f; v.Nasality = 0.08f; v.Breathiness = 0.12f;
        }),
        P("builtin-young-adult-man-20s", "Young Adult Man (20s)", PresetCategories.Age, "🧑", "A clear, neutral young-adult masculine voice with only the slightest lift from baseline.", new[] { "age", "young-adult", "man", "neutral" }, v =>
        {
            v.PitchSemitones = 0.5f; v.FormantSemitones = 0.2f;
        }),
        P("builtin-young-adult-woman-20s", "Young Adult Woman (20s)", PresetCategories.Age, "🌸", "A clear, brighter young-adult feminine voice ideal for casual chat and content creation.", new[] { "age", "young-adult", "woman", "bright" }, v =>
        {
            v.PitchSemitones = 6f; v.FormantSemitones = 2.6f; v.Breathiness = 0.12f;
        }),
        P("builtin-middle-aged-man-50", "Middle-Aged Man (50)", PresetCategories.Age, "🧔", "A slightly deeper, grounded adult masculine voice, a subtle age-up from a young man's tone.", new[] { "age", "middle-aged", "man", "grounded" }, v =>
        {
            v.PitchSemitones = -1f; v.FormantSemitones = -0.5f; v.LowShelfDb = 1.5f;
        }),
        P("builtin-middle-aged-woman-50", "Middle-Aged Woman (50)", PresetCategories.Age, "🍷", "A warm, grounded middle-aged feminine voice with softened highs for a mature, confident tone.", new[] { "age", "middle-aged", "woman", "warm" }, v =>
        {
            v.PitchSemitones = 5f; v.FormantSemitones = 2.2f;
        }),
        P("builtin-old-man-75", "Old Man (75+)", PresetCategories.Age, "👴", "A frail, gravelly elderly masculine voice with a natural vocal tremor and softened top end.", new[] { "age", "elderly", "old-man", "tremor", "raspy" }, v =>
        {
            v.PitchSemitones = -1.5f; v.FormantSemitones = -0.8f;
            v.TremorRateHz = 5.8f; v.TremorPitchDepth = 0.25f; v.TremorAmpDepth = 0.2f;
            v.Breathiness = 0.3f; v.Rasp = 0.15f; v.HighShelfDb = -4f; v.LowPassHz = 9200;
        }),
        P("builtin-old-woman-75", "Old Woman (75+)", PresetCategories.Age, "👵", "A frail, breathy elderly feminine voice with gentle vocal tremor, perfect for grandmotherly characters.", new[] { "age", "elderly", "old-woman", "tremor", "breathy" }, v =>
        {
            v.PitchSemitones = 4.5f; v.FormantSemitones = 2f;
            v.TremorRateHz = 6f; v.TremorPitchDepth = 0.22f; v.TremorAmpDepth = 0.18f;
            v.Breathiness = 0.3f; v.HighShelfDb = -4f;
        }),

        // ── Professional ──
        P("builtin-customer-support-agent", "Customer Support Agent", PresetCategories.Professional, "☎️", "A clean, friendly, highly intelligible voice tuned for customer support and phone calls.", new[] { "professional", "support", "clarity", "phone" }, v =>
        {
            v.HighPassHz = 100; v.Peak1Hz = 250; v.Peak1Db = -2f; v.Peak1Q = 1.2f; v.Peak3Hz = 3000; v.Peak3Db = 2.5f; v.HighShelfDb = 1.5f;
            v.DeEsserEnabled = true; v.DeEsserAmount = 0.4f;
            v.CompRatio = 3.5f; v.CompThresholdDb = -20f; v.CompMakeupDb = 3f; v.GateThresholdDb = -46f;
        }),
        P("builtin-podcast-host", "Podcast Host", PresetCategories.Professional, "🎙️", "A warm, broadcast-ready voice with presence and gentle compression for podcasting.", new[] { "professional", "podcast", "broadcast", "warm" }, v =>
        {
            v.HighPassHz = 100; v.Peak1Hz = 250; v.Peak1Db = -2f; v.Peak1Q = 1.2f; v.Peak3Hz = 3000; v.Peak3Db = 2.5f; v.LowShelfDb = 1f; v.HighShelfDb = 1.5f;
            v.DeEsserEnabled = true; v.DeEsserAmount = 0.4f;
            v.CompRatio = 3f; v.CompThresholdDb = -18f; v.CompMakeupDb = 3f; v.GateThresholdDb = -46f;
        }),
        P("builtin-news-anchor", "News Anchor", PresetCategories.Professional, "📰", "An authoritative, articulate broadcast voice tuned for news delivery.", new[] { "professional", "news", "anchor", "authoritative" }, v =>
        {
            v.PitchSemitones = -0.5f; v.FormantSemitones = -0.3f;
            v.HighPassHz = 100; v.Peak1Hz = 250; v.Peak1Db = -2f; v.Peak1Q = 1.2f; v.Peak3Hz = 3000; v.Peak3Db = 2.5f; v.HighShelfDb = 1.5f;
            v.DeEsserEnabled = true; v.DeEsserAmount = 0.4f;
            v.CompRatio = 4f; v.CompThresholdDb = -18f; v.CompMakeupDb = 3.5f; v.GateThresholdDb = -46f;
        }),
        P("builtin-audiobook-narrator", "Audiobook Narrator", PresetCategories.Professional, "📖", "A smooth, controlled reading voice with gentle compression to stay consistent over long passages.", new[] { "professional", "audiobook", "narrator", "smooth" }, v =>
        {
            v.HighPassHz = 100; v.Peak1Hz = 250; v.Peak1Db = -2f; v.Peak1Q = 1.2f; v.Peak3Hz = 3000; v.Peak3Db = 2.5f; v.HighShelfDb = 1.5f;
            v.DeEsserEnabled = true; v.DeEsserAmount = 0.4f;
            v.CompRatio = 3f; v.CompThresholdDb = -20f; v.CompMakeupDb = 3f; v.CompReleaseMs = 140f; v.GateThresholdDb = -46f;
        }),
        P("builtin-corporate-presenter", "Corporate Presenter", PresetCategories.Professional, "💼", "A polished, confident presentation voice suited for webinars and corporate video.", new[] { "professional", "corporate", "presenter", "polished" }, v =>
        {
            v.PitchSemitones = 0.5f; v.FormantSemitones = 0.3f;
            v.HighPassHz = 100; v.Peak1Hz = 250; v.Peak1Db = -2f; v.Peak1Q = 1.2f; v.Peak3Hz = 3000; v.Peak3Db = 2.5f; v.HighShelfDb = 1.5f;
            v.DeEsserEnabled = true; v.DeEsserAmount = 0.4f;
            v.CompRatio = 3.5f; v.CompThresholdDb = -19f; v.CompMakeupDb = 3f; v.GateThresholdDb = -46f;
        }),
        P("builtin-esports-caster", "E-Sports Caster", PresetCategories.Professional, "🎮", "An energetic, punchy commentary voice with forward presence that cuts through game audio.", new[] { "professional", "esports", "caster", "energetic" }, v =>
        {
            v.PitchSemitones = 1f; v.FormantSemitones = 0.5f;
            v.HighPassHz = 100; v.Peak1Hz = 250; v.Peak1Db = -2f; v.Peak1Q = 1.2f; v.Peak2Hz = 1200; v.Peak2Db = 1.5f; v.Peak3Hz = 3000; v.Peak3Db = 2.5f; v.HighShelfDb = 1.5f;
            v.DeEsserEnabled = true; v.DeEsserAmount = 0.4f;
            v.CompRatio = 4f; v.CompThresholdDb = -18f; v.CompMakeupDb = 4f; v.GateThresholdDb = -46f;
        }),
        P("builtin-radio-dj", "Radio DJ", PresetCategories.Professional, "📻", "A punchy, broadcast-compressed FM-radio-style voice with boosted lows for maximum presence.", new[] { "professional", "radio", "dj", "punchy" }, v =>
        {
            v.PitchSemitones = -1f; v.FormantSemitones = -0.3f;
            v.HighPassHz = 100; v.Peak1Hz = 250; v.Peak1Db = -2f; v.Peak1Q = 1.2f; v.Peak3Hz = 3000; v.Peak3Db = 2.5f; v.LowShelfDb = 2f; v.HighShelfDb = 1.5f;
            v.DeEsserEnabled = true; v.DeEsserAmount = 0.4f;
            v.CompRatio = 4f; v.CompThresholdDb = -18f; v.CompMakeupDb = 4f; v.GateThresholdDb = -46f;
        }),
        P("builtin-asmr-whisper", "ASMR Whisper", PresetCategories.Professional, "🤫", "A soft, intimate close-mic whisper with extra breath and gentle compression for ASMR content.", new[] { "professional", "asmr", "whisper", "intimate" }, v =>
        {
            v.InputGainDb = 6f; v.Breathiness = 0.2f;
            v.HighPassHz = 100; v.Peak1Hz = 250; v.Peak1Db = -2f; v.Peak1Q = 1.2f; v.Peak3Hz = 3000; v.Peak3Db = 2.5f; v.HighShelfDb = 1.5f;
            v.DeEsserEnabled = true; v.DeEsserAmount = 0.4f;
            v.CompRatio = 4f; v.CompThresholdDb = -22f; v.CompMakeupDb = 3f; v.GateThresholdDb = -50f;
        }),
        P("builtin-conference-call-clear", "Conference Call Clear", PresetCategories.Professional, "💻", "A tightened, midrange-forward voice designed to stay intelligible over compressed video calls.", new[] { "professional", "conference", "clarity", "video-call" }, v =>
        {
            v.HighPassHz = 120; v.Peak1Hz = 250; v.Peak1Db = -2f; v.Peak1Q = 1.2f; v.Peak3Hz = 3000; v.Peak3Db = 2.5f; v.HighShelfDb = 1.5f; v.LowPassHz = 12000;
            v.DeEsserEnabled = true; v.DeEsserAmount = 0.4f;
            v.CompRatio = 4f; v.CompThresholdDb = -20f; v.CompMakeupDb = 3.5f; v.GateThresholdDb = -44f;
        }),
        P("builtin-voice-over-artist", "Voice-Over Artist", PresetCategories.Professional, "🎬", "A rich, controlled voice-over tone with clarity EQ for commercials and narration work.", new[] { "professional", "voice-over", "narration", "rich" }, v =>
        {
            v.PitchSemitones = -0.5f; v.FormantSemitones = -0.2f;
            v.HighPassHz = 100; v.Peak1Hz = 250; v.Peak1Db = -2f; v.Peak1Q = 1.2f; v.Peak3Hz = 3000; v.Peak3Db = 2.5f; v.HighShelfDb = 1.5f;
            v.DeEsserEnabled = true; v.DeEsserAmount = 0.4f;
            v.CompRatio = 3.5f; v.CompThresholdDb = -19f; v.CompMakeupDb = 3.5f; v.GateThresholdDb = -46f;
        }),

        // ── Accent Flavor ──
        P("builtin-british-broadcast", "British Broadcast Warmth", PresetCategories.Accent, "🇬🇧", "A warm, rounded broadcast tone inspired by classic British radio; this is a tonal flavor only and does not change pronunciation.", new[] { "accent", "british", "broadcast", "tonal-flavor", "warm" }, v =>
        {
            v.LowShelfDb = 2f; v.Peak2Hz = 1200; v.Peak2Db = 1f; v.HighShelfDb = 1f;
        }),
        P("builtin-american-fm-radio", "American FM Radio", PresetCategories.Accent, "🇺🇸", "A bright, punchy FM-radio coloration inspired by American broadcast tone; this is a tonal flavor only and does not change pronunciation.", new[] { "accent", "american", "fm-radio", "tonal-flavor", "bright" }, v =>
        {
            v.LowShelfDb = 1.5f; v.Peak3Db = 2f; v.HighShelfDb = 2f; v.CompRatio = 4f;
        }),
        P("builtin-australian-bright", "Australian Bright", PresetCategories.Accent, "🇦🇺", "An open, bright tonal coloration inspired by Australian broadcast warmth; this is a tonal flavor only and does not change pronunciation.", new[] { "accent", "australian", "bright", "tonal-flavor" }, v =>
        {
            v.Peak2Hz = 1400; v.Peak2Db = 1.5f; v.HighShelfDb = 2f;
        }),
        P("builtin-canadian-soft", "Canadian Soft", PresetCategories.Accent, "🇨🇦", "A gentle, softened tonal coloration inspired by Canadian broadcast warmth; this is a tonal flavor only and does not change pronunciation.", new[] { "accent", "canadian", "soft", "tonal-flavor" }, v =>
        {
            v.HighShelfDb = -1f; v.LowShelfDb = 1f; v.Peak1Db = -1f;
        }),
        P("builtin-irish-lilt-warmth", "Irish Lilt Warmth", PresetCategories.Accent, "🇮🇪", "A warm, lightly lifted resonance inspired by Irish vocal warmth; this is a tonal flavor only and does not change pronunciation.", new[] { "accent", "irish", "warmth", "tonal-flavor" }, v =>
        {
            v.PitchSemitones = 0.5f; v.Peak2Hz = 1100; v.Peak2Db = 1.5f; v.LowShelfDb = 1.5f;
        }),
        P("builtin-scottish-gravel", "Scottish Gravel", PresetCategories.Accent, "🏴", "A slightly rasped, robust tonal coloration inspired by Scottish vocal grit; this is a tonal flavor only and does not change pronunciation.", new[] { "accent", "scottish", "gravel", "tonal-flavor" }, v =>
        {
            v.Rasp = 0.12f; v.LowShelfDb = 2f; v.Peak2Hz = 1000; v.Peak2Db = 1f;
        }),
        P("builtin-french-nasal-velvet", "French Nasal Velvet", PresetCategories.Accent, "🇫🇷", "A smooth, softly nasal resonance inspired by French vocal color; this is a tonal flavor only and does not change pronunciation.", new[] { "accent", "french", "nasal", "velvet", "tonal-flavor" }, v =>
        {
            v.Nasality = 0.15f; v.HighShelfDb = 1f; v.Peak2Db = 1f;
        }),
        P("builtin-deep-south-warmth", "Deep South Warmth", PresetCategories.Accent, "🌻", "A slow, warm, low-toned coloration inspired by Deep South vocal warmth; this is a tonal flavor only and does not change pronunciation.", new[] { "accent", "deep-south", "warmth", "tonal-flavor" }, v =>
        {
            v.PitchSemitones = -1f; v.LowShelfDb = 2.5f; v.HighShelfDb = -1f;
        }),
        P("builtin-new-york-punch", "New York Punch", PresetCategories.Accent, "🗽", "A forward, punchy midrange coloration inspired by New York vocal energy; this is a tonal flavor only and does not change pronunciation.", new[] { "accent", "new-york", "punch", "tonal-flavor" }, v =>
        {
            v.Peak2Hz = 1500; v.Peak2Db = 2.5f; v.CompRatio = 4f; v.CompMakeupDb = 3f;
        }),
        P("builtin-nordic-cool", "Nordic Cool", PresetCategories.Accent, "❄️", "A cool, airy, understated tonal coloration inspired by Nordic vocal restraint; this is a tonal flavor only and does not change pronunciation.", new[] { "accent", "nordic", "cool", "tonal-flavor" }, v =>
        {
            v.HighShelfDb = 1.5f; v.LowShelfDb = -1f; v.Breathiness = 0.08f;
        }),
        P("builtin-transatlantic-vintage", "Transatlantic Vintage", PresetCategories.Accent, "📻", "A band-limited, vintage 1940s-radio coloration; this is a tonal flavor only and does not change pronunciation.", new[] { "accent", "vintage", "transatlantic", "radio", "tonal-flavor" }, v =>
        {
            v.RadioMix = 0.6f; v.RadioLowHz = 250; v.RadioHighHz = 5000;
        }),

        // ── Utility ──
        P("builtin-clean-microphone", "Clean Microphone", PresetCategories.Utility, "🎙️", "A fully transparent pass-through with just a gentle high-pass and noise gate to clean up your mic.", new[] { "utility", "clean", "transparent", "neutral" }, v =>
        {
            v.HighPassHz = 80; v.EqEnabled = false; v.CompressorEnabled = false;
        }),
        P("builtin-broadcast-clean", "Broadcast Clean", PresetCategories.Utility, "📡", "A light broadcast-style polish with subtle EQ and compression for everyday use.", new[] { "utility", "broadcast", "clean", "polish" }, v =>
        {
            v.HighPassHz = 100; v.Peak1Db = -1f; v.Peak3Db = 1.5f; v.HighShelfDb = 1f;
            v.CompThresholdDb = -22f; v.CompRatio = 2.5f; v.CompMakeupDb = 2f;
        }),
        P("builtin-noise-gate-only", "Noise Gate Only", PresetCategories.Utility, "🚪", "Only the noise gate is active, for cutting background hiss with zero tonal change.", new[] { "utility", "gate", "noise", "transparent" }, v =>
        {
            v.EqEnabled = false; v.CompressorEnabled = false; v.GateThresholdDb = -45f;
        }),
        P("builtin-whisper-boost", "Whisper Boost", PresetCategories.Utility, "🗣️", "Boosts a quiet whisper up to a usable level with heavy compression and a touch of breath.", new[] { "utility", "whisper", "boost", "quiet" }, v =>
        {
            v.InputGainDb = 6f; v.CompThresholdDb = -30f; v.CompRatio = 6f; v.CompMakeupDb = 6f; v.Breathiness = 0.1f;
        }),
        P("builtin-late-night-quiet", "Late Night Quiet", PresetCategories.Utility, "🌃", "A gentle level boost with soft compression for speaking quietly late at night without waking anyone.", new[] { "utility", "quiet", "gentle", "late-night" }, v =>
        {
            v.InputGainDb = 4f; v.CompThresholdDb = -26f; v.CompRatio = 2.5f; v.CompMakeupDb = 3f;
        }),
        P("builtin-bypass-reference", "Bypass Reference", PresetCategories.Utility, "⚪", "A fully neutral pass-through with every processing stage disabled except the safety limiter, for A/B reference.", new[] { "utility", "bypass", "neutral", "reference" }, v =>
        {
            v.GateEnabled = false; v.EqEnabled = false; v.CompressorEnabled = false;
        }),
    };
}
