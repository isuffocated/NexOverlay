using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace NexOverlay.App;

public partial class WelcomeWizardView : UserControl
{
    private readonly Step[] _steps =
    [
        new(
            "01",
            "OPEN AND CLOSE NEXOVERLAY",
            "CapsLock + Space arms the overlay. Hover the small handle at the top-center of the active monitor to open or close it.",
            "The same gesture works both ways. The handle prevents an accidental instant toggle."),

        new(
            "02",
            "RECENT ACTIVITY",
            "The left panel keeps quick context about the items you recently worked with.",
            "Hover a layout zone and NexOverlay gives that area more room without hiding the rest."),

        new(
            "03",
            "WORKSPACE OVERVIEW",
            "The right panel shows live counters for your saved Notes, Snippets and Files.",
            "This gives you a fast view of what exists in the current local workspace."),

        new(
            "04",
            "FOUR MODULES",
            "The four center buttons open Notes, Snippets, Files and Clips. Each module keeps its own accent and returns through the back button.",
            "Notes are longer text, Snips are reusable fragments, Files are external links and Clips is clipboard history."),

        new(
            "05",
            "CLIPS AND PINNING",
            "NexOverlay can keep copied text in CLIPS. Pin important entries so they stay above ordinary clipboard items.",
            "The tutorial opened CLIPS for you. The pin controls are highlighted right now."),

        new(
            "06",
            "COMMAND PALETTE",
            "The center search is also a command palette. It can open modules, run actions and search Notes, Snippets, Files and Clips.",
            "Use Up/Down to move through results, Enter to open the selected result and Escape to close the palette.")
    ];

    private int _index;

    public event EventHandler<int>? StepChanged;
    public event EventHandler? Completed;
    public event EventHandler? Skipped;

    public WelcomeWizardView()
    {
        InitializeComponent();
        BuildDots();
        RenderStep();
    }

    public int CurrentStepIndex =>
        _index;

    public void ReplayCurrentStep()
    {
        StepChanged?.Invoke(
            this,
            _index);
    }

    private void NextButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        if (_index >= _steps.Length - 1)
        {
            Completed?.Invoke(
                this,
                EventArgs.Empty);

            return;
        }

        _index++;
        RenderStep();

        StepChanged?.Invoke(
            this,
            _index);
    }

    private void BackButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        if (_index <= 0)
            return;

        _index--;
        RenderStep();

        StepChanged?.Invoke(
            this,
            _index);
    }

    private void SkipButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        Skipped?.Invoke(
            this,
            EventArgs.Empty);
    }

    private void BuildDots()
    {
        DotsPanel.Children.Clear();

        for (var i = 0; i < _steps.Length; i++)
        {
            DotsPanel.Children.Add(
                new Ellipse
                {
                    Width = 6,
                    Height = 6,
                    Margin =
                        new Thickness(
                            0,
                            0,
                            i == _steps.Length - 1
                                ? 0
                                : 6,
                            0)
                });
        }
    }

    private void RenderStep()
    {
        var step =
            _steps[_index];

        StepGlyph.Text =
            step.Glyph;

        StepTitle.Text =
            step.Title;

        StepBody.Text =
            step.Body;

        StepTip.Text =
            step.Tip;

        BackButton.IsEnabled =
            _index > 0;

        NextButtonText.Text =
            _index == _steps.Length - 1
                ? "GET STARTED"
                : "NEXT";

        var active =
            new SolidColorBrush(
                Color.FromRgb(
                    169,
                    216,
                    255));

        var inactive =
            new SolidColorBrush(
                Color.FromArgb(
                    46,
                    255,
                    255,
                    255));

        for (var i = 0; i < DotsPanel.Children.Count; i++)
        {
            if (DotsPanel.Children[i] is Ellipse dot)
            {
                dot.Fill =
                    i == _index
                        ? active
                        : inactive;
            }
        }
    }

    private sealed record Step(
        string Glyph,
        string Title,
        string Body,
        string Tip);
}