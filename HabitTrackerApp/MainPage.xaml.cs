using System.Collections.ObjectModel;
using Microsoft.Maui.Storage;
using System.Linq;
using System.ComponentModel;

using Plugin.Maui.Audio; // Pour le son
using Microsoft.Maui.Devices; // Pour la vibration ( Pas gerer par le PC) 
namespace HabitTrackerApp;

class Habit : INotifyPropertyChanged
{
    string name;
    bool isCompleted;
    public bool Counted { get; set; } // Variable "counted" que je peux lire (get) et changer (set)
    public string Name
    {
        get => name; // On prend la variable name 
        set
        {
            name = value; // On stocke "value" -> dans la variable "name"
            OnPropertyChanged(nameof(Name)); // Prevenir l'UI que le nom a changer 
        }
    }

    public bool IsCompleted
    {
        // Modifier la variable "IsCompleted" -> Permet de dire si l'habit est faite ou pas 
        get => isCompleted;
        set
        {
            isCompleted = value;
            OnPropertyChanged(nameof(IsCompleted));
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;

    void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}


public partial class MainPage : ContentPage
{
    ObservableCollection<Habit> habits = new();

    int streak = 0;
    int points = 0;
    int rewardThreshold = 50;
    int spinsAvailable = 0;

    Random random = new Random();

    // Constructeur 
    public MainPage()
    {
        InitializeComponent();

        HabitList.ItemsSource = habits;
        HistoryList.ItemsSource = LoadHistory();

        LoadHabits();
        LoadStreak();

        spinsAvailable = Preferences.Get("spins", 0);
        SpinLabel.Text = $"🎡 {spinsAvailable} spins";

        points = Preferences.Get("points", 0);
        PointsLabel.Text = $"💰 {points} points";

        _ = CheckStreak();

        UpdateProgress();
    }

    void SetPoints(int value)
    {
        points = Math.Max(0, value);
        Preferences.Set("points", points);
        PointsLabel.Text = $"💰 {points} points";
    }

    void LoadHabits()
    {
        string saved = Preferences.Get("habitList", "");

        if (!string.IsNullOrEmpty(saved))
        {
            foreach (var item in saved.Split(','))
            {
                var parts = item.Split('|');

                bool isCompleted = parts.Length > 1 && bool.Parse(parts[1]);

                habits.Add(new Habit
                {
                    Name = parts[0],
                    IsCompleted = isCompleted,
                    Counted = isCompleted 
                });
            }
        }
    }

    void LoadStreak()
    {
        streak = Preferences.Get("streak", 0);
        StreakLabel.Text = $"🔥 {streak} day streak";
    }

    void SaveHabits()
    {
        Preferences.Set(
            "habitList",
            string.Join(",", habits.Select(h => $"{h.Name}|{h.IsCompleted}")));
    }

    private async void OnAddHabitClicked(object sender, EventArgs e)
    {
        await PressEffect((View)sender); // attendre que l'animation soit terminer avant de continuer 

        string habit = await DisplayPromptAsync("New Habit", "Enter habit name:"); // on recupere ce que l'utilisateur ecrit ---> on le stocke dans la variable "habit"

        // Si l'utilisateur a bien entree un habit 
        if (!string.IsNullOrWhiteSpace(habit))
        {
            habits.Insert(0, new Habit { Name = habit }); // On transforme le texte en objet puis on l'ajoute a la liste 

            // Enregistrer et mettre a jour la liste 
            SaveHabits();
            UpdateProgress();
        }
    }

    private void OnDeleteHabit(object sender, EventArgs e)
    {
        var swipe = (SwipeItem)sender; // On traite sender comme un "SwipeItem"

        Habit habit = swipe.BindingContext as Habit; // Pour recuperer l'habit liée a ce bouton "delete"

        habits.Remove(habit); // On le retire de la liste 

        SaveHabits();
        UpdateProgress();
    }

    private async void OnEditHabit(object sender, EventArgs e) // async -> la methode peut attendre qlq chose sans bloquer le reste
    {
        var button = (Button)sender; // pour signaler que le sender c'est un boutton 

        // Recuperer les donnees liee a ce bouton -> stocker dans la variable "habit"
        Habit habit = button.BindingContext as Habit; // (BindingContext -> donne les donnees)


        string newName = await DisplayPromptAsync // ouvre une petite fenêtre (popup)
                                                  // await => attends que l’utilisateur finisse d'ecrire avant de continuer
                                                  // ce que l'utilisateur ce que l'utilisateur ecrit est stocke dans "newName"
            (
            "Edit Habit",
            "Modify habit:",
            initialValue: habit.Name
            );


        if (!string.IsNullOrWhiteSpace(newName)) // pas vide, pas null, pas des espaces
        {
            habit.Name = newName; // On enregistre l'habit entree par l'utilisateur dans le systeme 
            SaveHabits();

            HabitList.ItemsSource = null;
            HabitList.ItemsSource = habits;
        }
    }

    private async void OnHabitCheckedChanged(object sender, CheckedChangedEventArgs e)
    {
        var checkbox = (CheckBox)sender;
        Habit habit = checkbox.BindingContext as Habit;

        if (habit == null)
            return;

        if (e.Value && !habit.Counted)
        {
            await SpinLabel.ScaleTo(1.3, 100);
            await SpinLabel.ScaleTo(1, 100);

            if (habit.Name.Contains("Bonus"))
            {
                SetPoints(points + 50); 
                SetSpins(GetSpins() + 1);
            }
            else
            {
                SetPoints(points + 10); 
                SetSpins(GetSpins() + 1);
            }

            habit.Counted = true;
        }
        else if (!e.Value && habit.Counted)
        {
            if (habit.Name.Contains("Bonus"))
            {
                SetPoints(points - 50); 
                SetSpins(GetSpins() - 1);
            }
            else
            {
                SetPoints(points - 10); 
                SetSpins(GetSpins() - 1);
            }

            habit.Counted = false;
        }

        habit.IsCompleted = e.Value;

        SaveHabits();
        UpdateProgress();
        CheckReward();
    }

    void UpdateProgress()
    {
        int total = habits.Count;
        int completed = habits.Count(h => h.IsCompleted);

        ProgressLabel.Text = $"{completed} / {total} habits completed";


    }

    private async void OnCompleteDayClicked(object sender, EventArgs e)
    {
        await PressEffect((View)sender); // On attend que l’effet soit terminé AVANT de continuer la suite du code

        int total = habits.Count;
        int completed = habits.Count(h => h.IsCompleted);

        if (total == 0)
            return; // Si il n'y a pas d'habitude completer on ne retourne rien

        var today = DateTime.Now.Date; // Date d'aujourd'hui (Mise a jour)

        var lastDateString = Preferences.Get("lastDate", "");

        // Empêche de refaire le jour plusieurs fois

        if (!string.IsNullOrEmpty(lastDateString)) // Regarde si il ya une date enregistrer 
        {
            var lastDate = DateTime.Parse(lastDateString).Date; //Transformer le texte en Date

            //Si la derniere date c'est aujourd'hui -> Empecher de completer la journee a nouveau 
            if (lastDate == today)
            {
                await DisplayAlert("Déjà fait", "Tu as déjà complété aujourd’hui 🔥", "OK");
                return;
            }
        }

        // Message utilisateur Si toutes les taches n'ont pas ete completer 
        if (completed < total)
        {
            await DisplayAlert(
                "Day completed ⚠️",
                $"Tu as fait {completed}/{total} tâches.\nEssaie de tout finir demain 💪",
                "OK");
        }

        //Sinon toutes les taches ont ete completer 
        else
        {
            await DisplayAlert("Perfect 🔥", "Toutes les tâches complétées!", "OK");
        }


        // Gestion du streak basée sur la vraie date

        if (!string.IsNullOrEmpty(lastDateString)) // Regarde si il ya une date enregistrer 
        {
            var lastDate = DateTime.Parse(lastDateString).Date;
            int diff = (today - lastDate).Days; // La difference entre aujourd'hui et la derniere date 

            // Si la diff est 1 -> On est au jour suivant => On incremente le compteur 
            if (diff == 1)
                streak++;
            // Si la diff est > 1 -> On a depasser le jour suivant => La serie est casser
            else if (diff > 1)
                streak = 1;
        }

        else
        {
            streak = 1; // Sinon pas de diff => c'est la premier connexion
        }

        // Preferences -> petit stockage (comme une mémoire)

        Preferences.Set("streak", streak);
        Preferences.Set("lastDate", today.ToString("yyyy-MM-dd"));

        // Affiche le nbre de jours consecutif ou tu t'es connecter 
        StreakLabel.Text = $"🔥 {streak} day streak";

        var completedHabits = habits.Where(h => h.IsCompleted).ToList(); // Transforme le resultat en une vrai liste 

        foreach (var habit in completedHabits) // On parcourt chaque tache completer -> pour le retirer dans la liste creer precedemment 
        {
            habits.Remove(habit);
        }

        foreach (var habit in habits) // On parcourt la liste "de base" -> On remet à 0 pour repartir propre chaque jour
        {
            habit.IsCompleted = false;
            habit.Counted = false;
        }

        bool allDone = completed == total;

        SaveDayHistory(true);

        HistoryList.ItemsSource = LoadHistory();

        SaveHabits();
        UpdateProgress();
    }
    void SaveDayHistory(bool completedAll)
    {
        var history = Preferences.Get("history", ""); // Preferences -> petit stockage (comme une mémoire) => ".Get" il recupere l'historique 

        string today = DateTime.Now.ToString("yyyy-MM-dd"); // On prend la date d'aujourd'hui et on le convertit en texte 

        string newEntry = $"{today}|{completedAll}"; // On crée un texte avec plusieurs infos collées ensemble (Ex: 2026-04-02|True)

        // Si l'historique est vide on met "newEntry"
        if (string.IsNullOrEmpty(history))
            history = newEntry;

        // Si l'historique contient deja les entrees pour aujourd'hui on fait rien
        if (history.Contains(today))
            return;

        // Sinon on ajoute les nouvelles donnees 
        else
            history += "," + newEntry;

        Preferences.Set("history", history); // Preferences -> petit stockage (comme une mémoire) => ".Set" il stocke l'historique 
    }

    // Cree une liste qui contient les jours de la semaine
    // static -> variable partager par toute la classe => "readonly" impossible de modifier après
    static readonly List<DayOfWeek> WeekOrder = new()
{
    DayOfWeek.Sunday,
    DayOfWeek.Monday,
    DayOfWeek.Tuesday,
    DayOfWeek.Wednesday,
    DayOfWeek.Thursday,
    DayOfWeek.Friday,
    DayOfWeek.Saturday
};

    List<DayHistory> LoadHistory()
    {
        var list = new List<DayHistory>(); // La variable "list" contient l'historique des jours 

        string saved = Preferences.Get("history", ""); // La variable "saved"  -> contient les elements preleves dans "l'historique"

        // Si la variable "saved" n'est pas vide => on rentre dans la condition 
        if (!string.IsNullOrEmpty(saved))
        {
            foreach (var item in saved.Split(',')) // On parcourt tout les elements dans "saved" -> On les separe avec une "," => On les stocke dans "item"
            {
                var parts = item.Split('|'); // On separe les elements contenu dans "item" avec une "|" 

                list.Add // On ajoute des elements a la variable "List"

                    (new DayHistory // nouvel objet avec 2 infos (Date, Completed)

                    {
                        Date = parts[0], // la première valeur (avant le |)
                        Completed = bool.Parse(parts[1]) // la deuxième valeur -> transformer en true/false
                    }

                    );
            }
        }

        for (int i = 0; i < 7; i++) // Une boucle pour les 7 jours 
        {
            var date = DateTime.Now.AddDays(-i).ToString("yyyy-MM-dd"); // On remonte a une semaine en arriere (7 jours) -> Transforme cet objet en date 

            if (!list.Any(x => x.Date == date)) // Si la date generer n'existe pas deja dans la liste 
            {
                list.Add // Cree une variable "list"

                    (new DayHistory // Cree un objet DayHistory avec 2 elements (Date, Completed)
                    {
                        Date = date,
                        Completed = false // On initialise a "faux"
                    }
                    );
            }
        }

        return list
    .GroupBy(x => DateTime.Parse(x.Date).DayOfWeek)
    .Select(g => g.OrderByDescending(x => x.Date).First())

    .OrderBy(x =>
    {
        var date = DateTime.Parse(x.Date);
        return WeekOrder.IndexOf(date.DayOfWeek);
    })
    .ToList();
    }

    class DayHistory
    {
        public string Date { get; set; }
        public bool Completed { get; set; }
        public string Display
        {
            get
            {
                var date = DateTime.Parse(Date);
                var today = DateTime.Now.Date;

                if (date == today)
                {
                    string history = Preferences.Get("history", "");

                    if (!history.Contains(Date))
                        return $"{date:dddd} ⏳";
                }

                return $"{date:ddd dd} {(Completed ? "✅" : "❌")}";
            }
        }
    }

    void ResetHabitsForNextDay()
    {
        foreach (var habit in habits)
        {
            habit.IsCompleted = false;
        }

        HabitList.ItemsSource = null;
        HabitList.ItemsSource = habits;

        SaveHabits();
        UpdateProgress();
    }

    async void CheckReward()
    {
        if (points >= 50 && points < 100)
            RewardLabel.Text = "🥉 Bronze";

        else if (points >= 100 && points < 200)
            RewardLabel.Text = "🥈 Silver";

        else if (points >= 200)
            RewardLabel.Text = "🥇 Gold";
        else
            RewardLabel.Text = "";

        if (points >= rewardThreshold)
        {
            rewardThreshold += 50;

            await DisplayAlert("🎉 Reward!", $"Tu as atteint {points} points 🔥", "OK");

            await RewardLabel.ScaleTo(1.3, 100);
            await RewardLabel.ScaleTo(1, 100);


            PointsLabel.TextColor = Colors.Gold;

        }

    }

    void ApplyReward(int segment)
    {
        switch (segment)
        {
            case 0:
                SetPoints(points + 20);
                break;

            case 1:
                streak += 1;
                break;

            case 2:
                var habit = habits.FirstOrDefault(h => !h.IsCompleted);
                if (habit != null)
                    habit.IsCompleted = true;
                break;

            case 3:
                SetPoints(points + 50);
                break;

            case 4:
                SetSpins(GetSpins() + 2); 
                break;

            case 5:
                SetPoints(points + 10);
                break;

            case 6:
                SetSpins(GetSpins() + 1); 
                break;

            case 7:
                streak += 2;
                break;
        }

        PointsLabel.Text = $"💰 {points} points";
        StreakLabel.Text = $"🔥 {streak} day streak";

        UpdateProgress();
    }

    bool isSpinning = false;



    private async void OnSpinClicked(object sender, EventArgs e)
    {
        Console.WriteLine($"[CLICK] isSpinning={isSpinning}, spins={GetSpins()}");

        if (isSpinning)

            return;

        if (GetSpins() <= 0)
        {
            Console.WriteLine("[BLOCK] No spins");
            await DisplayAlert("No spins", "Complete habits to earn spins 🔥", "OK");
            return;
        }

        isSpinning = true;
        Console.WriteLine("[STATE] isSpinning = TRUE");
        SpinButton.IsEnabled = false;

        try
        {
            Console.WriteLine("SPIN START");

            SetSpins(GetSpins() - 1); 

            var button = (Button)sender;
            await button.ScaleTo(0.9, 100);
            await button.ScaleTo(1, 100);

            WheelImage.CancelAnimations();
            WheelImage.Rotation = 0;

            int rotations = random.Next(4, 7);
            int finalAngle = random.Next(0, 360);
            int totalRotation = rotations * 360 + finalAngle;


            Console.WriteLine("[ANIMATION] Start rotation");

            var spinTask = WheelImage.RotateTo(totalRotation, 3500, Easing.CubicOut);
            var timeoutTask = Task.Delay(4000);

            var completedTask = await Task.WhenAny(spinTask, timeoutTask);

            if (completedTask == timeoutTask)
            {
                Console.WriteLine("⚠️ Animation timeout (MAUI bug évité)");
            }
            else
            {
                Console.WriteLine("[ANIMATION] End rotation");
            }

            WheelImage.Rotation = finalAngle;

            int correctedAngle = (360 - finalAngle + 22) % 360;
            int segment = correctedAngle / 45;

            Console.WriteLine($"[RESULT] segment={segment}");

            ApplyReward(segment);
            await ShowReward(segment);

            Console.WriteLine("SPIN END OK");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERREUR SPIN: {ex}");
            await DisplayAlert("Erreur", "Bug pendant le spin", "OK");
        }
        finally
        {
            isSpinning = false;
            Console.WriteLine("[UI] Button disabled");

            SpinButton.IsEnabled = true;
            Console.WriteLine("[UI] Button enabled");

            Console.WriteLine("[RESET] isSpinning = FALSE");
        }
    }

    async Task ShowReward(int segment)
    {
        string reward = segment switch
        {
            0 => "💰 +20 points",
            1 => "🎁 Bonus reward",
            2 => "⚡ Speed boost",
            3 => "💎 +50 points",
            4 => "🎡 +2 spins", 
            5 => "💰 +10 points",
            6 => "🎡 +1 spin", 
            7 => "🔥 Streak boost",
            _ => "Nothing"
        };

        await DisplayAlert("🎡 Result", reward, "OK");
    }

    async Task CheckStreak()
    {
        string lastDateString = Preferences.Get("lastDate", "");

        if (string.IsNullOrEmpty(lastDateString))
            return;

        DateTime lastDate = DateTime.Parse(lastDateString);
        DateTime today = DateTime.Now.Date;

        int diff = (today - lastDate.Date).Days;

        if (diff == 1)
            return;

        if (diff > 1 && lastDate.Date != today)
        {
            streak = 0;
            SetPoints(0);
            SetSpins(0);

            Preferences.Set("streak", streak);
            Preferences.Set("points", points);
            Preferences.Set("spins", spinsAvailable);

            await DisplayAlert("Streak lost 😢", "Tu as cassé ta série!", "OK");

            StreakLabel.Text = $"🔥 {streak} day streak";
            PointsLabel.Text = $"💰 {points} points";
            SpinLabel.Text = $"🎡 {spinsAvailable} spins";
        }
    }

    async Task PressEffect(View view)
    {
        await view.ScaleTo(0.9, 80, Easing.CubicOut);
        await view.ScaleTo(1, 80, Easing.CubicIn);
    }

    async Task PlaySound(string fileName)
    {
        var player = AudioManager.Current.CreatePlayer
            (
        await FileSystem.OpenAppPackageFileAsync(fileName)
            );

        player.Play();
    }

    int GetSpins()
    {
        return Preferences.Get("spins", 0);
    }

    void SetSpins(int value)
    {
        spinsAvailable = Math.Max(0, value);
        Preferences.Set("spins", spinsAvailable);
        SpinLabel.Text = $"🎡 {spinsAvailable} spins";
    }
}



