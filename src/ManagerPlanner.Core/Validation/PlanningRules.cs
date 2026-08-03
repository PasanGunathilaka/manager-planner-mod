namespace ManagerPlanner.Core.Validation;

/// <summary>
/// Thrown by <see cref="PlanningRules"/> validators when a business rule is violated.
/// </summary>
public class ValidationException : Exception
{
    public ValidationException(string message) : base(message)
    {
    }
}

/// <summary>
/// Business-rule validators ported from the legacy
/// ExecutivePlanning.Core/Services/PlanningValidation.cs. Each ceiling/window is exposed as a
/// public const so later features can reference it directly instead of a magic number.
/// </summary>
public static class PlanningRules
{
    public const int MaxProjectName = 120;
    public const int MaxTaskTitle = 120;
    public const int MaxObjectiveTitle = 150;
    public const int MaxChecklistLabel = 300;
    public const int MaxNoteText = 2000;
    public const int NoteBackdateMonths = 1;

    public static void ValidateProjectName(string? name)
    {
        var t = name?.Trim() ?? string.Empty;
        if (t.Length == 0)
        {
            throw new ValidationException("Project name is required.");
        }

        if (t.Length > MaxProjectName)
        {
            throw new ValidationException($"Project name cannot exceed {MaxProjectName} characters.");
        }
    }

    public static void ValidateTaskTitle(string? title)
    {
        var t = title?.Trim() ?? string.Empty;
        if (t.Length == 0)
        {
            throw new ValidationException("Task title is required.");
        }

        if (t.Length > MaxTaskTitle)
        {
            throw new ValidationException($"Task title cannot exceed {MaxTaskTitle} characters.");
        }
    }

    public static void ValidateObjectiveTitle(string? title)
    {
        var t = title?.Trim() ?? string.Empty;
        if (t.Length == 0)
        {
            throw new ValidationException("Objective title is required.");
        }

        if (t.Length > MaxObjectiveTitle)
        {
            throw new ValidationException($"Objective title cannot exceed {MaxObjectiveTitle} characters.");
        }
    }

    public static void ValidateChecklistLabel(string? label)
    {
        var t = label?.Trim() ?? string.Empty;
        if (t.Length == 0)
        {
            throw new ValidationException("Checklist label is required.");
        }

        if (t.Length > MaxChecklistLabel)
        {
            throw new ValidationException($"Checklist label cannot exceed {MaxChecklistLabel} characters.");
        }
    }

    public static void ValidateNoteText(string? text)
    {
        var t = text?.Trim() ?? string.Empty;
        if (t.Length == 0)
        {
            throw new ValidationException("The note is empty — type what was said before saving.");
        }

        if (t.Length > MaxNoteText)
        {
            throw new ValidationException($"The note is too long. Keep it under {MaxNoteText} characters.");
        }
    }

    public static void ValidateNoteDate(DateTime noteDateUtc, DateTime? nowUtc = null)
    {
        var today = (nowUtc ?? DateTime.UtcNow).Date;
        var earliestAllowed = today.AddMonths(-NoteBackdateMonths);
        var d = noteDateUtc.Date;

        if (d < earliestAllowed)
        {
            throw new ValidationException($"That date is more than a month back. Notes can only be dated on or after {earliestAllowed:MMM dd, yyyy}.");
        }

        if (d > today)
        {
            throw new ValidationException("A note cannot be dated in the future.");
        }
    }
}
