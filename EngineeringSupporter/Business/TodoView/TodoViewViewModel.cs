using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Collections.Specialized;
using System.Runtime.CompilerServices;
using System.Linq;
using System.Windows.Input;
using System.Globalization;
using EngineeringSupporter.DB;
using EngineeringSupporter.DB.Entity.Todo;
using Microsoft.EntityFrameworkCore;

namespace EngineeringSupporter.Business.TodoView;

public sealed class TodoViewViewModel : INotifyPropertyChanged
{
    private const double DeleteColumnWidth = 90;
    private const double ColumnSpacing = 10;
    private const int MovingAverageWindow = 3;
    private const int TaskNameIndex = 0;
    private const int PlanStartIndex = 1;
    private const int PlanEndIndex = 2;
    private const int PredictionEndIndex = 3;
    private const int PlannedProgressIndex = 4;
    private const int ActualStartIndex = 5;
    private const int ActualEndIndex = 6;
    private const int ProgressIndex = 7;
    private const int ForecastIndex = 8;
    private const int DeleteIndex = 9;
    public ObservableCollection<TodoViewColumn> Columns { get; } = new();
    public ObservableCollection<TodoViewRow> Rows { get; } = new();
    private readonly HashSet<TodoViewCell> _trackedProgressCells = new();
    private AppDbContext? _dbContext;
    private bool _tasksLoaded;
    private bool _tasksLoading;
    private bool _databaseInitialized;
    private static readonly HashSet<DateTime> Holidays = new();

    private string _newTaskName = string.Empty;
    private DateTime _newPlanStartDate = DateTime.Today;
    private DateTime _newPlanEndDate = DateTime.Today;
    private string _newProgress = string.Empty;

    public string NewTaskName
    {
        get => _newTaskName;
        set => SetField(ref _newTaskName, value);
    }

    public DateTime NewPlanStartDate
    {
        get => _newPlanStartDate;
        set => SetField(ref _newPlanStartDate, value);
    }

    public DateTime NewPlanEndDate
    {
        get => _newPlanEndDate;
        set => SetField(ref _newPlanEndDate, value);
    }

    public string NewProgress
    {
        get => _newProgress;
        set => SetField(ref _newProgress, value);
    }

    public ICommand AddRowCommand { get; }

    public TodoViewViewModel(AppDbContext? dbContext)
    {
        _dbContext = dbContext;
        InitializeColumns();
        AddRowCommand = new Command(async () => await AddRowAsync());
        Rows.CollectionChanged += OnRowsChanged;
        foreach (var row in Rows)
        {
            AttachProgressTracking(row);
        }
        if (_dbContext is not null)
        {
            _ = InitializeFromDatabaseAsync();
        }
    }

    public void SetDbContext(AppDbContext dbContext)
    {
        if (_dbContext is not null)
        {
            return;
        }

        _dbContext = dbContext;
        _ = InitializeFromDatabaseAsync();
    }

    private void InitializeColumns()
    {
        Columns.Clear();
        Columns.Add(new TodoViewColumn(global::EngineeringSupporter.Resources.Localize.Resources.todo_table_header_taskname, 220));
        Columns.Add(new TodoViewColumn(global::EngineeringSupporter.Resources.Localize.Resources.todo_table_header_planstart, 140));
        Columns.Add(new TodoViewColumn(global::EngineeringSupporter.Resources.Localize.Resources.todo_table_header_planend, 140));
        Columns.Add(new TodoViewColumn(global::EngineeringSupporter.Resources.Localize.Resources.todo_table_header_predictionend, 150));
        Columns.Add(new TodoViewColumn(global::EngineeringSupporter.Resources.Localize.Resources.todo_table_header_todayplanprogress, 170));
        Columns.Add(new TodoViewColumn(global::EngineeringSupporter.Resources.Localize.Resources.todo_table_header_actualstart, 140));
        Columns.Add(new TodoViewColumn(global::EngineeringSupporter.Resources.Localize.Resources.todo_table_header_actualend, 140));
        Columns.Add(new TodoViewColumn(global::EngineeringSupporter.Resources.Localize.Resources.todo_table_header_progress, 120));
        Columns.Add(new TodoViewColumn(global::EngineeringSupporter.Resources.Localize.Resources.todo_table_header_progressforecast, 170));
        Columns.Add(new TodoViewColumn(global::EngineeringSupporter.Resources.Localize.Resources.todo_table_header_delete, DeleteColumnWidth, isFixedWidth: true));
    }

    private async Task AddRowAsync()
    {
        if (string.IsNullOrWhiteSpace(NewTaskName))
        {
            return;
        }

        var row = new TodoViewRow();
        var taskNameCell = new TodoViewCell(NewTaskName.Trim(), Columns[TaskNameIndex].Width);
        var planStartCell = new TodoViewCell(NewPlanStartDate.ToString("yyyy-MM-dd"), Columns[PlanStartIndex].Width);
        var planEndCell = new TodoViewCell(NewPlanEndDate.ToString("yyyy-MM-dd"), Columns[PlanEndIndex].Width);
        var plannedProgressPercent = CalculatePlannedProgressPercent(NewPlanStartDate, NewPlanEndDate, DateTime.Today);
        var predictionEndCell = new TodoViewCell(string.Empty, Columns[PredictionEndIndex].Width);
        var todayPlanProgressCell = new TodoViewCell($"{plannedProgressPercent}%", Columns[PlannedProgressIndex].Width);
        var actualStartCell = new TodoViewCell(string.Empty, Columns[ActualStartIndex].Width);
        var actualEndCell = new TodoViewCell(string.Empty, Columns[ActualEndIndex].Width);
        var progressCell = new TodoViewCell(NewProgress.Trim(), Columns[ProgressIndex].Width, isEditable: true);
        var progressForecastCell = new TodoViewCell(string.Empty, Columns[ForecastIndex].Width);
        var deleteCell = new TodoViewCell(string.Empty, Columns[DeleteIndex].Width)
        {
            IsDeleteAction = true
        };

        row.Cells.Add(taskNameCell);
        row.Cells.Add(planStartCell);
        row.Cells.Add(planEndCell);
        row.Cells.Add(predictionEndCell);
        row.Cells.Add(todayPlanProgressCell);
        row.Cells.Add(actualStartCell);
        row.Cells.Add(actualEndCell);
        row.Cells.Add(progressCell);
        row.Cells.Add(progressForecastCell);
        row.Cells.Add(deleteCell);

        row.PlannedProgressPercent = plannedProgressPercent;
        row.DeleteCommand = new Command(async () => await DeleteRowAsync(row));
        deleteCell.Command = row.DeleteCommand;
        UpdateScheduleStatus(row, progressCell);
        TrackProgressForActualStart(row, progressCell, actualStartCell, actualEndCell);
        UpdateForecast(row);

        await PersistTaskAsync(row, taskNameCell.DisplayText, NewPlanStartDate, NewPlanEndDate);
        await PersistProgressAsync(row, progressCell.DisplayText);

        Rows.Add(row);
        NewTaskName = string.Empty;
        NewProgress = string.Empty;
    }

    private void OnRowsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is null)
        {
            return;
        }

        foreach (var item in e.NewItems)
        {
            if (item is TodoViewRow row)
            {
                AttachProgressTracking(row);
            }
        }
    }

    private void AttachProgressTracking(TodoViewRow row)
    {
        if (row.Cells.Count <= ProgressIndex)
        {
            return;
        }

        if (row.DeleteCommand is null)
        {
            row.DeleteCommand = new Command(async () => await DeleteRowAsync(row));
        }

        var plannedProgressCell = row.Cells[PlannedProgressIndex];
        var actualStartCell = row.Cells[ActualStartIndex];
        var actualEndCell = row.Cells[ActualEndIndex];
        var progressCell = row.Cells[ProgressIndex];

        if (row.PlannedProgressPercent == 0)
        {
            var plannedValue = GetProgressValue(plannedProgressCell.DisplayText);
            row.PlannedProgressPercent = plannedValue > 0 ? plannedValue : 0;
        }

        if (_trackedProgressCells.Contains(progressCell))
        {
            return;
        }

        _trackedProgressCells.Add(progressCell);
        UpdateScheduleStatus(row, progressCell);
        TrackProgressForActualStart(row, progressCell, actualStartCell, actualEndCell);
    }

    private void TrackProgressForActualStart(TodoViewRow row, TodoViewCell progressCell, TodoViewCell actualStartCell, TodoViewCell actualEndCell)
    {
        var hadProgress = !string.IsNullOrWhiteSpace(progressCell.DisplayText);
        var lastProgress = GetProgressValue(progressCell.DisplayText);
        progressCell.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName != nameof(TodoViewCell.DisplayText))
            {
                return;
            }

            var hasProgress = !string.IsNullOrWhiteSpace(progressCell.DisplayText);
            if (!hadProgress && hasProgress && string.IsNullOrWhiteSpace(actualStartCell.DisplayText))
            {
                actualStartCell.DisplayText = DateTime.Today.ToString("yyyy-MM-dd");
            }

            var currentProgress = GetProgressValue(progressCell.DisplayText);
            if (currentProgress == 100 && lastProgress != 100 && string.IsNullOrWhiteSpace(actualEndCell.DisplayText))
            {
                actualEndCell.DisplayText = DateTime.Today.ToString("yyyy-MM-dd");
            }

            UpdateScheduleStatus(row, progressCell);
            UpdateProgressHistory(row, DateOnly.FromDateTime(DateTime.Today), currentProgress);
            UpdateForecast(row);
            hadProgress = hasProgress;
            lastProgress = currentProgress;
        };
    }

    public async Task CommitProgressAsync(TodoViewRow row, TodoViewCell progressCell)
    {
        UpdateScheduleStatus(row, progressCell);
        await PersistProgressAsync(row, progressCell.DisplayText);
    }

    private async Task PersistTaskAsync(TodoViewRow row, string taskName, DateTime planStartDate, DateTime planEndDate)
    {
        if (_dbContext is null)
        {
            return;
        }

        EnsureDatabaseReady();

        var entity = new TaskEntity
        {
            TaskName = taskName,
            PlanStartDate = DateOnly.FromDateTime(planStartDate),
            PlanEndDate = DateOnly.FromDateTime(planEndDate),
            PredictionEndDate = DateOnly.FromDateTime(planEndDate)
        };

        _dbContext.TaskEntities.Add(entity);
        await _dbContext.SaveChangesAsync();
        row.TaskId = entity.TaskId;
    }

    private async Task PersistProgressAsync(TodoViewRow row, string? progressText)
    {
        if (_dbContext is null)
        {
            return;
        }

        EnsureDatabaseReady();

        if (row.TaskId <= 0)
        {
            await EnsureTaskForRowAsync(row);
        }

        TaskEntity? taskEntity = null;
        if (row.TaskId > 0)
        {
            taskEntity = await _dbContext.TaskEntities.FindAsync(row.TaskId);
            if (taskEntity is null)
            {
                row.TaskId = 0;
                await EnsureTaskForRowAsync(row);
                if (row.TaskId > 0)
                {
                    taskEntity = await _dbContext.TaskEntities.FindAsync(row.TaskId);
                }
            }
        }

        if (row.TaskId <= 0 || taskEntity is null)
        {
            return;
        }

        var progressValue = GetProgressValue(progressText);
        if (progressValue < 0)
        {
            return;
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        var existing = await _dbContext.TaskProgressManagementEntities
            .FirstOrDefaultAsync(item => item.TaskId == row.TaskId && item.ProgressDate == today);

        if (existing is null)
        {
            _dbContext.TaskProgressManagementEntities.Add(new TaskProgressManagementEntity
            {
                TaskId = row.TaskId,
                TaskEntity = taskEntity,
                Progress = progressValue,
                ProgressDate = today
            });
        }
        else
        {
            existing.Progress = progressValue;
        }

        await _dbContext.SaveChangesAsync();
        await RefreshProgressFromDbAsync(row);
    }

    private async Task RefreshProgressFromDbAsync(TodoViewRow row)
    {
        if (_dbContext is null || row.TaskId <= 0 || row.Cells.Count <= ProgressIndex)
        {
            return;
        }

        var latest = await _dbContext.TaskProgressManagementEntities
            .AsNoTracking()
            .Where(entry => entry.TaskId == row.TaskId)
            .OrderBy(entry => entry.ProgressDate)
            .LastOrDefaultAsync();

        if (latest is null)
        {
            row.Cells[ProgressIndex].DisplayText = string.Empty;
            return;
        }

        row.Cells[ProgressIndex].DisplayText = $"{latest.Progress:0}";
        UpdateProgressHistory(row, latest.ProgressDate, latest.Progress);
        UpdateForecast(row);
    }

    private async Task EnsureTaskForRowAsync(TodoViewRow row)
    {
        if (_dbContext is null || row.TaskId > 0 || row.Cells.Count <= PlanEndIndex)
        {
            return;
        }

        var taskName = row.Cells[TaskNameIndex].DisplayText.Trim();
        if (string.IsNullOrWhiteSpace(taskName))
        {
            return;
        }

        var planStart = ParseDateOnlyOrToday(row.Cells[PlanStartIndex].DisplayText);
        var planEnd = ParseDateOnlyOrToday(row.Cells[PlanEndIndex].DisplayText);

        var entity = new TaskEntity
        {
            TaskName = taskName,
            PlanStartDate = planStart,
            PlanEndDate = planEnd,
            PredictionEndDate = planEnd
        };

        _dbContext.TaskEntities.Add(entity);
        await _dbContext.SaveChangesAsync();
        row.TaskId = entity.TaskId;
    }

    private static DateOnly ParseDateOnlyOrToday(string? value)
    {
        if (!string.IsNullOrWhiteSpace(value) &&
            DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return parsed;
        }

        return DateOnly.FromDateTime(DateTime.Today);
    }

    private void EnsureDatabaseReady()
    {
        if (_dbContext is null || _databaseInitialized)
        {
            return;
        }

        if (!_dbContext.Database.GetMigrations().Any())
        {
            _dbContext.Database.EnsureCreated();
        }
        else
        {
            _dbContext.Database.Migrate();
        }

        _databaseInitialized = true;
    }

    private async Task InitializeFromDatabaseAsync()
    {
        if (_dbContext is null || _tasksLoaded || _tasksLoading)
        {
            return;
        }

        _tasksLoading = true;
        EnsureDatabaseReady();
        await LoadExistingTasksAsync();
        _tasksLoaded = true;
        _tasksLoading = false;
    }

    private async Task LoadExistingTasksAsync()
    {
        if (_dbContext is null)
        {
            return;
        }

        var tasks = await _dbContext.TaskEntities
            .AsNoTracking()
            .Include(task => task.TaskProgressManagementEntities)
            .OrderBy(task => task.TaskId)
            .ToListAsync();

        foreach (var task in tasks)
        {
            var planStart = task.PlanStartDate.ToDateTime(TimeOnly.MinValue);
            var planEnd = task.PlanEndDate.ToDateTime(TimeOnly.MinValue);
            var plannedPercent = CalculatePlannedProgressPercent(planStart, planEnd, DateTime.Today);

            var progressEntries = task.TaskProgressManagementEntities
                .OrderBy(entry => entry.ProgressDate)
                .ToList();

            var latestProgress = progressEntries.LastOrDefault();
            var progressText = latestProgress is null ? string.Empty : $"{latestProgress.Progress:0}";

            var actualStart = progressEntries.FirstOrDefault(entry => entry.Progress > 0)?.ProgressDate;
            var actualEnd = progressEntries.FirstOrDefault(entry => entry.Progress >= 100)?.ProgressDate;

            var row = new TodoViewRow
            {
                TaskId = task.TaskId,
                PlannedProgressPercent = plannedPercent
            };
            row.DeleteCommand = new Command(async () => await DeleteRowAsync(row));

            row.Cells.Add(new TodoViewCell(task.TaskName, Columns[TaskNameIndex].Width));
            row.Cells.Add(new TodoViewCell(planStart.ToString("yyyy-MM-dd"), Columns[PlanStartIndex].Width));
            row.Cells.Add(new TodoViewCell(planEnd.ToString("yyyy-MM-dd"), Columns[PlanEndIndex].Width));
            row.Cells.Add(new TodoViewCell(string.Empty, Columns[PredictionEndIndex].Width));
            row.Cells.Add(new TodoViewCell($"{plannedPercent}%", Columns[PlannedProgressIndex].Width));
            row.Cells.Add(new TodoViewCell(actualStart?.ToString("yyyy-MM-dd") ?? string.Empty, Columns[ActualStartIndex].Width));
            row.Cells.Add(new TodoViewCell(actualEnd?.ToString("yyyy-MM-dd") ?? string.Empty, Columns[ActualEndIndex].Width));
            row.Cells.Add(new TodoViewCell(progressText, Columns[ProgressIndex].Width, isEditable: true));
            row.Cells.Add(new TodoViewCell(string.Empty, Columns[ForecastIndex].Width));
            var deleteCell = new TodoViewCell(string.Empty, Columns[DeleteIndex].Width)
            {
                IsDeleteAction = true,
                Command = row.DeleteCommand
            };
            row.Cells.Add(deleteCell);

            foreach (var entry in progressEntries)
            {
                row.ProgressHistory.Add(new TaskProgressEntry(entry.ProgressDate, entry.Progress));
            }

            var progressCell = row.Cells[ProgressIndex];
            UpdateScheduleStatus(row, progressCell);
            TrackProgressForActualStart(row, progressCell, row.Cells[ActualStartIndex], row.Cells[ActualEndIndex]);
            UpdateForecast(row);

            Rows.Add(row);
        }
    }

    public void UpdateColumnWidths(double availableWidth)
    {
        if (Columns.Count == 0)
        {
            return;
        }

        var fixedWidthTotal = Columns.Where(column => column.IsFixedWidth).Sum(column => column.Width);
        var usableWidth = availableWidth - fixedWidthTotal - ColumnSpacing;
        if (usableWidth <= 0)
        {
            return;
        }

        var flexibleColumns = Columns.Count(column => !column.IsFixedWidth);
        var totalSpacing = ColumnSpacing * Math.Max(Columns.Count - 1, 0);
        var perColumn = (usableWidth - totalSpacing) / flexibleColumns;
        if (perColumn <= 0)
        {
            return;
        }

        foreach (var column in Columns)
        {
            if (!column.IsFixedWidth)
            {
                column.Width = perColumn;
            }
        }

        foreach (var row in Rows)
        {
            for (var i = 0; i < Columns.Count && i < row.Cells.Count; i++)
            {
                row.Cells[i].Width = Columns[i].Width;
            }
        }
    }

    private static int GetProgressValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return -1;
        }

        var trimmed = value.Trim().Replace("%", string.Empty);
        return int.TryParse(trimmed, out var parsed) ? parsed : -1;
    }

    private static void UpdateScheduleStatus(TodoViewRow row, TodoViewCell progressCell)
    {
        var progress = GetProgressValue(progressCell.DisplayText);
        if (progress < 0)
        {
            progress = 0;
        }

        row.IsBehindSchedule = progress < row.PlannedProgressPercent;
        row.IsAheadSchedule = progress >= row.PlannedProgressPercent + 10;
    }

    private static void UpdateProgressHistory(TodoViewRow row, DateOnly date, double progress)
    {
        var existing = row.ProgressHistory.FirstOrDefault(entry => entry.ProgressDate == date);
        if (existing is not null)
        {
            existing.Progress = progress;
            return;
        }

        row.ProgressHistory.Add(new TaskProgressEntry(date, progress));
    }

    private void UpdateForecast(TodoViewRow row)
    {
        if (row.Cells.Count <= ForecastIndex)
        {
            return;
        }

        if (row.ProgressHistory.Count == 0)
        {
            row.Cells[ForecastIndex].DisplayText = string.Empty;
            row.Cells[PredictionEndIndex].DisplayText = string.Empty;
            return;
        }

        var ordered = row.ProgressHistory
            .OrderBy(entry => entry.ProgressDate)
            .ToList();

        var last = ordered[^1];
        var currentProgress = last.Progress;
        var delta = ordered.Count == 1 ? currentProgress : CalculateMovingAverageDelta(ordered);
        var forecast = Math.Clamp(currentProgress + delta, 0, 100);
        row.Cells[ForecastIndex].DisplayText = $"{forecast:0}% ({delta:+0;-0;0}%)";

        if (delta <= 0)
        {
            row.Cells[PredictionEndIndex].DisplayText = string.Empty;
            return;
        }

        var remaining = 100 - currentProgress;
        if (remaining <= 0)
        {
            row.Cells[PredictionEndIndex].DisplayText = last.ProgressDate.ToString("yyyy-MM-dd");
            return;
        }

        var daysNeeded = (int)Math.Ceiling(remaining / delta);
        var predictionDate = AddBusinessDays(last.ProgressDate, daysNeeded);
        row.Cells[PredictionEndIndex].DisplayText = predictionDate.ToString("yyyy-MM-dd");
    }

    private static double CalculateMovingAverageDelta(IReadOnlyList<TaskProgressEntry> orderedHistory)
    {
        if (orderedHistory.Count < 2)
        {
            return 0;
        }

        var startIndex = Math.Max(1, orderedHistory.Count - MovingAverageWindow);
        var sum = 0.0;
        var count = 0;

        for (var i = startIndex; i < orderedHistory.Count; i++)
        {
            var previous = orderedHistory[i - 1];
            var current = orderedHistory[i];
            var days = CountBusinessDaysExclusive(previous.ProgressDate.ToDateTime(TimeOnly.MinValue),
                current.ProgressDate.ToDateTime(TimeOnly.MinValue));
            if (days <= 0)
            {
                continue;
            }

            sum += (current.Progress - previous.Progress) / days;
            count++;
        }

        return count == 0 ? 0 : sum / count;
    }

    private async Task DeleteRowAsync(TodoViewRow row)
    {
        Rows.Remove(row);

        if (_dbContext is null || row.TaskId <= 0)
        {
            return;
        }

        EnsureDatabaseReady();

        var taskEntity = await _dbContext.TaskEntities
            .Include(task => task.TaskProgressManagementEntities)
            .FirstOrDefaultAsync(task => task.TaskId == row.TaskId);

        if (taskEntity is null)
        {
            return;
        }

        if (taskEntity.TaskProgressManagementEntities.Count > 0)
        {
            _dbContext.TaskProgressManagementEntities.RemoveRange(taskEntity.TaskProgressManagementEntities);
        }

        _dbContext.TaskEntities.Remove(taskEntity);
        await _dbContext.SaveChangesAsync();
    }

    private static int CalculatePlannedProgressPercent(DateTime planStart, DateTime planEnd, DateTime today)
    {
        var start = planStart.Date;
        var end = planEnd.Date;
        var current = today.Date;

        if (end < start)
        {
            return 0;
        }

        if (current < start)
        {
            return 0;
        }

        if (current > end)
        {
            return 100;
        }

        var totalBusinessDays = CountBusinessDays(start, end);
        if (totalBusinessDays <= 0)
        {
            return 0;
        }

        var elapsedBusinessDays = CountBusinessDays(start, current);
        var percentage = (int)Math.Round((elapsedBusinessDays / (double)totalBusinessDays) * 100, MidpointRounding.AwayFromZero);
        return Math.Clamp(percentage, 0, 100);
    }

    private static int CountBusinessDays(DateTime start, DateTime end)
    {
        if (end < start)
        {
            return 0;
        }

        var count = 0;
        for (var date = start.Date; date <= end.Date; date = date.AddDays(1))
        {
            if (IsBusinessDay(date))
            {
                count++;
            }
        }

        return count;
    }

    private static int CountBusinessDaysExclusive(DateTime start, DateTime end)
    {
        if (end <= start)
        {
            return 0;
        }

        var count = 0;
        for (var date = start.Date.AddDays(1); date <= end.Date; date = date.AddDays(1))
        {
            if (IsBusinessDay(date))
            {
                count++;
            }
        }

        return count;
    }

    private static DateOnly AddBusinessDays(DateOnly start, int businessDays)
    {
        if (businessDays <= 0)
        {
            return start;
        }

        var date = start;
        var added = 0;
        while (added < businessDays)
        {
            date = date.AddDays(1);
            if (IsBusinessDay(date.ToDateTime(TimeOnly.MinValue)))
            {
                added++;
            }
        }

        return date;
    }

    private static bool IsBusinessDay(DateTime date)
    {
        if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            return false;
        }

        return !Holidays.Contains(date.Date);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string propertyName = "")
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private void SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
    {
        if (Equals(field, value))
        {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName);
    }
}

public sealed class TodoViewColumn : INotifyPropertyChanged
{
    public string Title { get; }
    private double _width;
    private bool _isFixedWidth;

    public double Width
    {
        get => _width;
        set => SetField(ref _width, value);
    }

    public bool IsFixedWidth
    {
        get => _isFixedWidth;
        set => SetField(ref _isFixedWidth, value);
    }

    public TodoViewColumn(string title, double width, bool isFixedWidth = false)
    {
        Title = title;
        _width = width;
        _isFixedWidth = isFixedWidth;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string propertyName = "")
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private void SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
    {
        if (Equals(field, value))
        {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName);
    }
}

public sealed class TodoViewRow : INotifyPropertyChanged
{
    public ObservableCollection<TodoViewCell> Cells { get; } = new();
    public List<TaskProgressEntry> ProgressHistory { get; } = new();
    private bool _isBehindSchedule;
    private bool _isAheadSchedule;
    private int _plannedProgressPercent;
    private int _taskId;
    private ICommand? _deleteCommand;

    public bool IsBehindSchedule
    {
        get => _isBehindSchedule;
        set => SetField(ref _isBehindSchedule, value);
    }

    public bool IsAheadSchedule
    {
        get => _isAheadSchedule;
        set => SetField(ref _isAheadSchedule, value);
    }

    public int PlannedProgressPercent
    {
        get => _plannedProgressPercent;
        set => SetField(ref _plannedProgressPercent, value);
    }

    public int TaskId
    {
        get => _taskId;
        set => SetField(ref _taskId, value);
    }

    public ICommand? DeleteCommand
    {
        get => _deleteCommand;
        set => SetField(ref _deleteCommand, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string propertyName = "")
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private void SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
    {
        if (Equals(field, value))
        {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName);
    }
}

public sealed class TodoViewCell : INotifyPropertyChanged
{
    private string _displayText;
    private double _width;
    private bool _isDeleteAction;
    private ICommand? _command;

    public string DisplayText
    {
        get => _displayText;
        set => SetField(ref _displayText, value);
    }

    public double Width
    {
        get => _width;
        set => SetField(ref _width, value);
    }
    public bool IsEditable { get; }

    public bool IsDeleteAction
    {
        get => _isDeleteAction;
        set => SetField(ref _isDeleteAction, value);
    }

    public ICommand? Command
    {
        get => _command;
        set => SetField(ref _command, value);
    }

    public TodoViewCell(string displayText, double width, bool isEditable = false)
    {
        _displayText = displayText;
        _width = width;
        IsEditable = isEditable;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string propertyName = "")
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private void SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
    {
        if (Equals(field, value))
        {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName);
    }
}

public sealed class TaskProgressEntry
{
    public DateOnly ProgressDate { get; }
    public double Progress { get; set; }

    public TaskProgressEntry(DateOnly progressDate, double progress)
    {
        ProgressDate = progressDate;
        Progress = progress;
    }
}
