using System.Collections.ObjectModel;
using Microsoft.Maui.Storage;
using System.Linq;

namespace HabitTrackerApp;

class Habit
{
    public string Name { get; set; } = "";
    public bool IsCompleted { get; set; }
    public int TotalCompletions { get; set; }
}

public partial class MainPage : ContentPage
{
    ObservableCollection<Habit> habits = new();

    int streak = 0;

    public MainPage()
    {
        InitializeComponent();

        HabitList.ItemsSource = habits;

        LoadHabits();
        LoadStreak();

        UpdateProgress();
    }

    void LoadHabits()
    {
        string saved = Preferences.Get("habitList", "");

        if (!string.IsNullOrEmpty(saved))
        {
            foreach (var item in saved.Split(','))
            {
                var parts = item.Split('|');

                habits.Add(new Habit
                {
                    Name = parts[0],
                    IsCompleted = parts.Length > 1 && bool.Parse(parts[1])
                });
            }
        }
    }

    void LoadStreak()
    {
        streak = Preferences.Get("streak", 0);
        StreakLabel.Text = $"🔥 {streak} day streak";
    }

    private async void OnAddHabitClicked(object sender, EventArgs e)
    {
        string habit = await DisplayPromptAsync("New Habit", "Enter habit name:");

        if (!string.IsNullOrWhiteSpace(habit))
        {
            habits.Add(new Habit { Name = habit });

            SaveHabits();
            UpdateProgress();
        }
    }

    private void OnDeleteHabit(object sender, EventArgs e)
    {
        var swipe = (SwipeItem)sender;
        Habit habit = swipe.BindingContext as Habit;

        habits.Remove(habit);

        SaveHabits();
        UpdateProgress();
    }

    private async void OnEditHabit(object sender, EventArgs e)
    {
        var button = (Button)sender;
        Habit habit = button.BindingContext as Habit;

        string newName = await DisplayPromptAsync(
            "Edit Habit",
            "Modify habit:",
            initialValue: habit.Name);

        if (!string.IsNullOrWhiteSpace(newName))
        {
            habit.Name = newName;

            SaveHabits();

            HabitList.ItemsSource = null;
            HabitList.ItemsSource = habits;
        }
    }

    private async void OnHabitCheckedChanged(object sender, CheckedChangedEventArgs e)
    {
        SaveHabits();

        await ProgressBar.ProgressTo(
            habits.Count(h => h.IsCompleted) / (double)habits.Count,
            250,
            Easing.CubicInOut);

        UpdateProgress();
    }

    void SaveHabits()
    {
        Preferences.Set(
            "habitList",
            string.Join(",", habits.Select(h => $"{h.Name}|{h.IsCompleted}")));
    }

    void UpdateProgress()
    {
        int total = habits.Count;
        int completed = habits.Count(h => h.IsCompleted);

        ProgressLabel.Text = $"{completed} / {total} habits completed";

        if (total > 0)
            ProgressBar.Progress = (double)completed / total;

        if (total > 0 && completed == total)
        {
            streak++;
            Preferences.Set("streak", streak);
            StreakLabel.Text = $"🔥 {streak} day streak";
        }
    }
}