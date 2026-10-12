using Android.Graphics;
using Android.Text;
using Android.Views;
using Android.Views.InputMethods;
using Android.Widget;
using VoiceChanger.Core.Presets;

namespace VoiceChangerPro.AndroidApp.Ui;

/// <summary>Voices tab: search, category chips and the preset list. Tapping a voice applies it everywhere.</summary>
public sealed class VoicesPage : Page
{
    private static readonly (string Label, string? Category)[] Filters =
    {
        ("All", null),
        ("Female", PresetCategories.Female),
        ("Male", PresetCategories.Male),
        ("Age", PresetCategories.Age),
        ("Professional", PresetCategories.Professional),
        ("Accent", PresetCategories.Accent),
        ("Utility", PresetCategories.Utility),
    };

    private sealed record PresetRow(VoicePreset Preset, LinearLayout View, TextView Check);

    private readonly LinearLayout _root;
    private readonly EditText _search;
    private readonly List<(TextView View, string? Category)> _chips = new();
    private readonly List<PresetRow> _rows = new();
    private readonly Dictionary<string, TextView> _headers = new();
    private readonly TextView _empty;
    private readonly ScrollView _scroll;
    private string? _category;

    public VoicesPage(MainActivity host) : base(host)
    {
        var c = host;
        _root = UiKit.Column(c);
        _root.LayoutParameters = new ViewGroup.LayoutParams(UiKit.Match, UiKit.Match);

        // ── Search ──
        _search = new EditText(c)
        {
            Hint = $"Search {State.Presets.Count} voices",
            InputType = InputTypes.ClassText | InputTypes.TextVariationFilter,
            ImeOptions = ImeAction.Search,
        };
        _search.SetSingleLine(true);
        _search.SetTextColor(Palette.Text);
        _search.SetHintTextColor(Palette.Muted);
        _search.SetTextSize(Android.Util.ComplexUnitType.Sp, 15);
        _search.Background = UiKit.Rounded(c, Palette.Raised, 12, Palette.Border);
        _search.SetPadding(UiKit.Dp(c, 16), UiKit.Dp(c, 12), UiKit.Dp(c, 16), UiKit.Dp(c, 12));
        _search.TextChanged += (_, _) => ApplyFilter();
        _search.EditorAction += (_, e) =>
        {
            e.Handled = true;
            HideKeyboard();
        };
        _root.AddView(_search, UiKit.Lp(c, UiKit.Match, UiKit.Wrap, 16, 8, 16, 4));

        // ── Category chips ──
        var chipScroll = new HorizontalScrollView(c) { HorizontalScrollBarEnabled = false, OverScrollMode = OverScrollMode.Never };
        var chipRow = UiKit.Row(c);
        chipRow.SetPadding(UiKit.Dp(c, 16), UiKit.Dp(c, 8), UiKit.Dp(c, 8), UiKit.Dp(c, 8));
        foreach (var (label, category) in Filters)
        {
            var chip = UiKit.Text(c, label, 14, Palette.Text, bold: true);
            chip.Gravity = GravityFlags.Center;
            chip.SetPadding(UiKit.Dp(c, 16), 0, UiKit.Dp(c, 16), 0);
            chip.Clickable = true;
            chip.Click += (_, _) => { _category = category; UpdateChips(); ApplyFilter(); _scroll?.ScrollTo(0, 0); };
            _chips.Add((chip, category));
            chipRow.AddView(chip, UiKit.Lp(c, UiKit.Wrap, UiKit.Dp(c, 36), 0, 0, 8));
        }
        chipScroll.AddView(chipRow);
        _root.AddView(chipScroll, UiKit.Lp(UiKit.Match, UiKit.Wrap));

        // ── Preset list ──
        _scroll = new ScrollView(c) { OverScrollMode = OverScrollMode.IfContentScrolls };
        var list = UiKit.Column(c);
        list.SetPadding(UiKit.Dp(c, 16), UiKit.Dp(c, 4), UiKit.Dp(c, 16), UiKit.Dp(c, 16));
        foreach (var category in PresetCategories.All)
        {
            var presets = State.Presets.Where(p => p.Category == category).ToList();
            if (presets.Count == 0) continue;
            var header = UiKit.Caption(c, $"{category}  ·  {presets.Count}");
            _headers[category] = header;
            list.AddView(header, UiKit.Lp(c, UiKit.Match, UiKit.Wrap, 4, 12, 0, 8));
            foreach (var preset in presets)
            {
                var row = BuildRow(c, preset);
                _rows.Add(row);
                list.AddView(row.View);
            }
        }
        _empty = UiKit.Text(c, "No voices match your search.", 15, Palette.Muted);
        _empty.Gravity = GravityFlags.Center;
        _empty.Visibility = ViewStates.Gone;
        list.AddView(_empty, UiKit.Lp(c, UiKit.Match, UiKit.Wrap, 0, 32));
        _scroll.AddView(list, new ViewGroup.LayoutParams(UiKit.Match, UiKit.Wrap));
        _root.AddView(_scroll, UiKit.Lp(UiKit.Match, 0, 1));

        UpdateChips();
        UpdateActive();
    }

    public override View Root => _root;

    private PresetRow BuildRow(MainActivity c, VoicePreset preset)
    {
        var row = UiKit.Row(c);
        row.SetPadding(UiKit.Dp(c, 12), UiKit.Dp(c, 12), UiKit.Dp(c, 12), UiKit.Dp(c, 12));
        row.Clickable = true;
        row.Focusable = true;
        row.LayoutParameters = UiKit.Lp(c, UiKit.Match, UiKit.Wrap, 0, 0, 0, 8);

        var icon = UiKit.Text(c, preset.Icon, 24, Palette.Text);
        icon.Gravity = GravityFlags.Center;
        icon.Background = UiKit.Rounded(c, Palette.Raised, 12);
        row.AddView(icon, new LinearLayout.LayoutParams(UiKit.Dp(c, 46), UiKit.Dp(c, 46)));

        var text = UiKit.Column(c);
        var name = UiKit.Text(c, preset.Name, 15, Palette.Text, bold: true);
        name.SetSingleLine(true);
        name.Ellipsize = TextUtils.TruncateAt.End;
        var desc = UiKit.Text(c, preset.Description, 13, Palette.Muted);
        desc.SetMaxLines(2);
        desc.Ellipsize = TextUtils.TruncateAt.End;
        text.AddView(name);
        text.AddView(desc, UiKit.Lp(c, UiKit.Match, UiKit.Wrap, 0, 2));
        row.AddView(text, UiKit.Lp(c, 0, UiKit.Wrap, 12, 0, 8).WithWeight(1));

        var check = UiKit.Text(c, "✓", 20, Palette.Accent, bold: true);
        row.AddView(check);

        row.Click += (_, _) =>
        {
            HideKeyboard();
            if (State.ActivePreset.Id != preset.Id || State.IsModified) State.SelectPreset(preset);
            Host.ShowToast(Host.IsLive ? $"{preset.Icon} {preset.Name} is on" : $"{preset.Icon} {preset.Name} selected");
        };
        return new PresetRow(preset, row, check);
    }

    private void UpdateChips()
    {
        var c = Host;
        foreach (var (chip, category) in _chips)
        {
            bool selected = category == _category;
            chip.Background = UiKit.Pressable(selected
                ? UiKit.Rounded(c, Palette.Accent, 18)
                : UiKit.Rounded(c, Palette.Raised, 18, Palette.Border), UiKit.Rounded(c, Color.White, 18));
            chip.SetTextColor(selected ? Color.White : Palette.Text);
        }
    }

    private void ApplyFilter()
    {
        string q = (_search.Text ?? "").Trim().ToLowerInvariant();
        var visibleByCategory = new Dictionary<string, int>();
        int total = 0;
        foreach (var row in _rows)
        {
            bool visible = (_category == null || row.Preset.Category == _category)
                           && (q.Length == 0 || row.Preset.SearchText.Contains(q));
            row.View.Visibility = visible ? ViewStates.Visible : ViewStates.Gone;
            if (visible)
            {
                visibleByCategory[row.Preset.Category] = visibleByCategory.GetValueOrDefault(row.Preset.Category) + 1;
                total++;
            }
        }
        foreach (var (category, header) in _headers)
        {
            int count = visibleByCategory.GetValueOrDefault(category);
            header.Visibility = count > 0 ? ViewStates.Visible : ViewStates.Gone;
            header.Text = $"{category}  ·  {count}".ToUpperInvariant();
        }
        _empty.Visibility = total == 0 ? ViewStates.Visible : ViewStates.Gone;
    }

    private void UpdateActive()
    {
        var c = Host;
        foreach (var row in _rows)
        {
            bool active = row.Preset.Id == State.ActivePreset.Id;
            var bg = active
                ? UiKit.Rounded(c, Palette.AccentSoft, 14, Palette.Accent, 1.5f)
                : UiKit.Rounded(c, Palette.Card, 14, Palette.Border);
            row.View.Background = UiKit.Pressable(bg, UiKit.Rounded(c, Color.White, 14));
            row.Check.Visibility = active ? ViewStates.Visible : ViewStates.Invisible;
        }
    }

    public override void OnStateChanged(StateChange change)
    {
        if (change.HasFlag(StateChange.Preset)) UpdateActive();
    }

    public override void OnHidden() => HideKeyboard();

    private void HideKeyboard()
    {
        _search.ClearFocus();
        var imm = (InputMethodManager?)Host.GetSystemService(Android.Content.Context.InputMethodService);
        imm?.HideSoftInputFromWindow(_search.WindowToken, HideSoftInputFlags.None);
    }
}
