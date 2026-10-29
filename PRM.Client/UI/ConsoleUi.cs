using System.Globalization;

namespace PRM.Client.UI;

public static class ConsoleUi
{
    public const int BoxWidth = 46;

    public static void ClearScreen() => Console.Clear();

    public static void WriteHeader(string title, string? subtitle = null)
    {
        WriteBoxTop();
        var innerWidth = BoxWidth - 2;
        var titleLine = title.Length > innerWidth ? title[..innerWidth] : title;
        WriteBoxLine(titleLine.PadRight(innerWidth));
        if (!string.IsNullOrWhiteSpace(subtitle))
        {
            var sub = subtitle.Length > innerWidth ? subtitle[..innerWidth] : subtitle;
            WriteBoxLine(sub.PadRight(innerWidth));
        }
        WriteBoxBottom();
        Console.WriteLine();
    }

    public static void WriteWelcomeHeader(string name)
    {
        var now = DateTime.Now.ToString("dd-MMM-yyyy  HH:mm", CultureInfo.InvariantCulture);
        WriteHeader($"Welcome, {name}!", now);
    }

    public static void WriteDivider() => Console.WriteLine(new string('─', BoxWidth));

    public static string ReadLine(string prompt)
    {
        Console.Write(prompt);
        return Console.ReadLine()?.Trim() ?? string.Empty;
    }

    public static string ReadRequired(string prompt)
    {
        while (true)
        {
            var value = ReadLine(prompt);
            if (!string.IsNullOrWhiteSpace(value))
                return value;
            Console.WriteLine("This field is required.");
        }
    }

    public static string ReadPassword(string prompt)
    {
        Console.Write(prompt);
        var password = string.Empty;
        ConsoleKeyInfo key;
        do
        {
            key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Backspace && password.Length > 0)
            {
                password = password[..^1];
                Console.Write("\b \b");
            }
            else if (!char.IsControl(key.KeyChar))
            {
                password += key.KeyChar;
                Console.Write('*');
            }
        } while (key.Key != ConsoleKey.Enter);

        Console.WriteLine();
        return password;
    }

    public static int ReadMenuOption(string prompt, int min, int max)
    {
        while (true)
        {
            var input = ReadLine(prompt);
            if (int.TryParse(input, out var option) && option >= min && option <= max)
                return option;

            Console.WriteLine($"Please enter a number between {min} and {max}.");
        }
    }

    public static bool Confirm(string message)
    {
        while (true)
        {
            var input = ReadLine($"{message} [Y/N]: ").Trim().ToUpperInvariant();
            if (input == "Y")
                return true;
            if (input == "N")
                return false;

            ShowError("Please enter Y or N only.");
        }
    }

    /// <summary>Reads Y or N only — rejects any other input.</summary>
    public static bool ReadYesOrNo(string prompt = "Choice")
    {
        while (true)
        {
            var input = ReadLine($"{prompt} [Y/N]: ").Trim().ToUpperInvariant();
            if (input == "Y")
                return true;
            if (input == "N")
                return false;

            ShowError("Please enter Y or N only.");
        }
    }

    /// <summary>Returns null when Enter is pressed to keep the current value.</summary>
    public static int? ReadOptionalMenuChoice(string prompt, int min, int max)
    {
        while (true)
        {
            var input = ReadLine(prompt).Trim();
            if (string.IsNullOrEmpty(input))
                return null;

            if (int.TryParse(input, out var option) && option >= min && option <= max)
                return option;

            ShowError($"Please enter a number between {min} and {max}, or press Enter to keep current.");
        }
    }

    public static bool IsIntimetecEmail(string email) =>
        !string.IsNullOrWhiteSpace(email)
        && email.EndsWith("@intimetec.com", StringComparison.OrdinalIgnoreCase)
        && email.Length > "@intimetec.com".Length
        && !email.Contains(' ');

    public static string ReadIntimetecEmail(string prompt, string? current = null)
    {
        while (true)
        {
            var input = ReadLine(prompt);
            var email = string.IsNullOrWhiteSpace(input) ? current : input.Trim();
            if (string.IsNullOrWhiteSpace(email))
            {
                ShowError("Email is required.");
                continue;
            }

            if (IsIntimetecEmail(email))
                return email;

            ShowError("Email must be in the format user@intimetec.com");
        }
    }

    /// <summary>Returns true when user chose an action; save is true for S, false for B.</summary>
    public static bool ReadSaveOrBack(out bool save)
    {
        while (true)
        {
            var choice = ReadLine("Choice: ").Trim().ToUpperInvariant();
            if (choice == "S")
            {
                save = true;
                return true;
            }

            if (choice == "B")
            {
                save = false;
                return true;
            }

            ShowError("Please enter S to Save or B to go Back.");
        }
    }

    public static bool ReadYesOrBack(out bool confirm)
    {
        while (true)
        {
            var choice = ReadLine("Choice: ").Trim().ToUpperInvariant();
            if (choice is "Y" or "YES")
            {
                confirm = true;
                return true;
            }

            if (choice == "B")
            {
                confirm = false;
                return true;
            }

            ShowError("Please enter Y to confirm or B to go Back.");
        }
    }

    public static void Pause(string message = "Press Enter to continue...")
    {
        Console.WriteLine();
        ReadLine(message);
    }

    public static void ShowError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(message);
        Console.ResetColor();
    }

    public static void ShowSuccess(string message)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(message);
        Console.ResetColor();
    }

    public static void ShowApiError(string? error)
    {
        ShowError(error ?? "An unexpected error occurred.");
        Pause();
    }

    public static DateOnly? ReadDateOptional(string prompt, string placeholder = "DD-MM-YYYY")
    {
        var input = ReadLine($"{prompt} ({placeholder}) or press Enter to skip: ");
        if (string.IsNullOrWhiteSpace(input))
            return null;

        if (TryParseDate(input, out var date))
            return date;

        ShowError($"Invalid date format. Use {placeholder}.");
        return ReadDateOptional(prompt, placeholder);
    }

    public static DateOnly ReadDateRequired(string prompt, string placeholder = "DD-MM-YYYY")
    {
        while (true)
        {
            var input = ReadRequired($"{prompt} ({placeholder}): ");
            if (TryParseDate(input, out var date))
                return date;

            ShowError($"Invalid date format. Use {placeholder}.");
        }
    }

    public static DateOnly? ReadDateOrDefault(string prompt, DateOnly defaultValue, string placeholder = "DD-MM-YYYY")
    {
        var input = ReadLine($"{prompt} ({placeholder}) or press Enter for {FormatDate(defaultValue)}: ");
        if (string.IsNullOrWhiteSpace(input))
            return defaultValue;

        if (TryParseDate(input, out var date))
            return date;

        ShowError($"Invalid date format. Use {placeholder}.");
        return ReadDateOrDefault(prompt, defaultValue, placeholder);
    }

    public static bool TryParseDate(string input, out DateOnly date)
    {
        var formats = new[] { "dd-MM-yyyy", "d-M-yyyy", "dd/MM/yyyy", "yyyy-MM-dd" };
        return DateOnly.TryParseExact(input, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date)
               || DateOnly.TryParse(input, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }

    public static string FormatDate(DateOnly date) =>
        date.ToString("dd-MMM-yy", CultureInfo.InvariantCulture);

    public static DateOnly GetLastMonday()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var dayOfWeek = (int)today.DayOfWeek;
        var daysFromMonday = dayOfWeek == 0 ? 6 : dayOfWeek - 1;
        return today.AddDays(-daysFromMonday);
    }

    public static DateOnly Today => DateOnly.FromDateTime(DateTime.Today);

    public static DateOnly ToMonday(DateOnly date)
    {
        var dayOfWeek = (int)date.DayOfWeek;
        var daysFromMonday = dayOfWeek == 0 ? 6 : dayOfWeek - 1;
        return date.AddDays(-daysFromMonday);
    }

    public static bool IsFutureWeek(DateOnly weekStart) => ToMonday(weekStart) > Today;

    public static DateOnly ReadDateFromToday(string prompt)
    {
        while (true)
        {
            var date = ReadDateRequired(prompt);
            if (date >= Today)
                return date;

            ShowError("Date cannot be in the past.");
        }
    }

    public static DateOnly ReadEndDateAfterStart(DateOnly startDate, string prompt)
    {
        while (true)
        {
            var date = ReadDateRequired(prompt);
            if (date > startDate)
                return date;

            ShowError("End date must be after start date.");
        }
    }

    public static DateOnly ReadDateWithinRange(
        DateOnly rangeStart,
        DateOnly rangeEnd,
        string prompt,
        bool notBeforeToday = false)
    {
        while (true)
        {
            var date = ReadDateRequired(prompt);
            var minDate = notBeforeToday && rangeStart < Today ? Today : rangeStart;

            if (date >= minDate && date <= rangeEnd)
                return date;

            if (date < minDate)
                ShowError(notBeforeToday && minDate == Today
                    ? "Date cannot be in the past."
                    : $"Date must be on or after {FormatDate(minDate)}.");
            else
                ShowError($"Date must be on or before {FormatDate(rangeEnd)}.");
        }
    }

    public static DateOnly ReadEndDateWithinRange(
        DateOnly startDate,
        DateOnly rangeStart,
        DateOnly rangeEnd,
        string prompt)
    {
        while (true)
        {
            var date = ReadDateRequired(prompt);
            if (date > startDate && date >= rangeStart && date <= rangeEnd)
                return date;

            if (date <= startDate)
                ShowError("End date must be after start date.");
            else if (date < rangeStart || date > rangeEnd)
                ShowError($"Date must fall within {FormatDate(rangeStart)} to {FormatDate(rangeEnd)}.");
        }
    }

    public static int ReadUtilisationPercent(int defaultValue)
    {
        while (true)
        {
            var input = ReadLine($"Utilisation % ({defaultValue}): ");
            var pct = string.IsNullOrWhiteSpace(input)
                ? defaultValue
                : int.TryParse(input, out var parsed) ? parsed : -1;

            if (pct is >= 1 and <= 100)
                return pct;

            ShowError("Utilisation must be between 1 and 100.");
        }
    }

    public static int ReadIntInRange(string prompt, int min, int max)
    {
        while (true)
        {
            var input = ReadRequired(prompt);
            if (int.TryParse(input, out var value) && value >= min && value <= max)
                return value;

            ShowError($"Please enter a number between {min} and {max}.");
        }
    }

    public static bool TryParseDateStrict(string input, out DateOnly date)
    {
        if (TryParseDate(input, out date))
            return true;

        date = default;
        return false;
    }

    public static string FormatHealth(string health) => health.ToUpperInvariant() switch
    {
        "ONTRACK" or "ON_TRACK" => "ON TRACK",
        "ATTENTION" => "ATTENTION",
        "ATRISK" or "AT_RISK" => "AT RISK",
        _ => health.ToUpperInvariant()
    };

    public static string HealthIcon(string health) => health.ToUpperInvariant() switch
    {
        "ONTRACK" or "ON_TRACK" => "ON TRACK",
        "ATTENTION" => "ATTENTION",
        "ATRISK" or "AT_RISK" => "AT RISK",
        _ => health
    };

    public static string HealthDisplay(string health)
    {
        var normalized = health.Replace("_", "", StringComparison.OrdinalIgnoreCase).ToUpperInvariant();
        return normalized switch
        {
            "ONTRACK" => "ON TRACK",
            "ATTENTION" => "ATTENTION",
            "ATRISK" => "AT RISK",
            _ => health
        };
    }

    public static string HealthEmoji(string health)
    {
        var normalized = health.Replace("_", "", StringComparison.OrdinalIgnoreCase).ToUpperInvariant();
        return normalized switch
        {
            "ONTRACK" => "ON TRACK",
            "ATTENTION" => "ATTENTION",
            "ATRISK" => "AT RISK",
            _ => health
        };
    }

    public static string HealthWithEmoji(string health)
    {
        var normalized = health.Replace("_", "", StringComparison.OrdinalIgnoreCase).ToUpperInvariant();
        return normalized switch
        {
            "ONTRACK" => "[OK] ON TRACK",
            "ATTENTION" => "[!!] ATTENTION",
            "ATRISK" => "[XX] AT RISK",
            _ => health
        };
    }

    private static void WriteBoxLine(string content)
    {
        var innerWidth = BoxWidth - 2;
        var line = content.Length > innerWidth ? content[..innerWidth] : content.PadRight(innerWidth);
        Console.WriteLine($"║ {line} ║");
    }

    private static void WriteBoxBottom() =>
        Console.WriteLine($"╚{new string('═', BoxWidth)}╝");

    public static void WriteBoxTop() =>
        Console.WriteLine($"╔{new string('═', BoxWidth)}╗");
}
