using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using SiJenggo.App.Ui;
using SiJenggo.Game;
using SiJenggo.Presentation.Audio;

namespace SiJenggo.App.Screens;

/// <summary>Title: the logo printed like a sticker, the menu as a column of tilted stickers over the alley key art.</summary>
public sealed class TitleScreen : Screen
{
    public TitleScreen(MainWindow shell) : base(shell)
    {
        var logo = new SablonText("SI JENGGO", 96) { Misregister = 5 };
        var sub = new TextBlock
        {
            Text = "PREMAN BERHATI MALAIKAT",
            FontFamily = Palette.Body,
            FontWeight = FontWeight.Bold,
            FontSize = 30,
            LetterSpacing = 6,
            Foreground = Palette.KunyitBrush,
            Margin = new Thickness(6, -8, 0, 0),
        };
        var tagline = UiKit.Text(Strings.Get("title.tagline"), 20, Palette.KapurDim);
        tagline.MaxWidth = 520;
        tagline.HorizontalAlignment = HorizontalAlignment.Left;
        tagline.FontStyle = FontStyle.Italic;

        var menu = new StackPanel { Spacing = 0, Margin = new Thickness(0, 14, 0, 0) };
        int i = 0;
        void Item(string key, Action action, bool enabled = true)
        {
            var b = UiKit.Sticker(Strings.Get(key), () =>
            {
                Shell.Audio.Play(Sfx.UiConfirm, 0.8f, 0f);
                action();
            }, 25, i++ % 2 == 0 ? -1.6 : 1.1);
            b.Padding = new Thickness(12, 3);
            b.IsEnabled = enabled;
            menu.Children.Add(b);
        }

        Item("menu.story", () => Shell.Navigate(new SetupScreen(Shell, GameModeKind.Story)));
        Item("menu.arcade", () => Shell.Navigate(new SetupScreen(Shell, GameModeKind.Arcade)));
        Item("menu.survival", () => Shell.Navigate(new SetupScreen(Shell, GameModeKind.Survival)));
        Item("menu.bossrush", () => Shell.Navigate(new SetupScreen(Shell, GameModeKind.BossRush)));
        Item("menu.challenge", () => Shell.Navigate(new ChallengeScreen(Shell)));
        Item("menu.online", () => Shell.Navigate(new OnlineScreen(Shell)));
        Item("menu.progress", () => Shell.Navigate(new ProgressionScreen(Shell)));
        Item("menu.settings", () => Shell.Navigate(new SettingsScreen(Shell)));
        Item("menu.credits", () => Shell.Navigate(new CreditsScreen(Shell)));
        Item("menu.quit", () => Shell.Close());

        var left = new StackPanel
        {
            Margin = new Thickness(72, 40, 0, 0),
            Spacing = 6,
            Children = { logo, sub, tagline, menu },
        };

        var hint = UiKit.Text(Strings.Get("menu.hint"), 17, Palette.KapurDim);
        var credit = UiKit.Text(Strings.Get("credit"), 18, Palette.KapurBrush, FontWeight.SemiBold);
        var footer = new StackPanel
        {
            Margin = new Thickness(72, 0, 0, 22),
            VerticalAlignment = VerticalAlignment.Bottom,
            Spacing = 4,
            Children = { credit, hint },
        };

        // Language switch, top right.
        var languages = Strings.AvailableLanguages;
        var chips = new Control[languages.Count];
        for (int li = 0; li < chips.Length; li++)
        {
            var language = languages[li];
            chips[li] = UiKit.Chip(Localization.Code(language).ToUpperInvariant(), () => SetLanguage(language), Strings.Language == language);
        }

        var lang = UiKit.Row(8, chips);
        lang.HorizontalAlignment = HorizontalAlignment.Right;
        lang.VerticalAlignment = VerticalAlignment.Top;
        lang.Margin = new Thickness(0, 28, 36, 0);

        var records = UiKit.Text($"{Strings.Get("best")} Arcade {shell.Profile.ArcadeHighScore:N0}  ·  {Strings.Get("menu.survival")} {shell.Profile.SurvivalBestWave}", 17, Palette.KapurDim);
        records.HorizontalAlignment = HorizontalAlignment.Right;
        records.VerticalAlignment = VerticalAlignment.Bottom;
        records.Margin = new Thickness(0, 0, 36, 36);

        Root.Children.Add(left);
        Root.Children.Add(footer);
        Root.Children.Add(lang);
        Root.Children.Add(records);
    }

    private void SetLanguage(Language language)
    {
        Shell.Profile.Settings.Language = language;
        Shell.ApplySettings();
        Shell.SaveProfile();
        Shell.Navigate(new TitleScreen(Shell));
    }

    public override void OnShown() => Shell.Audio.StopMusic();
}
