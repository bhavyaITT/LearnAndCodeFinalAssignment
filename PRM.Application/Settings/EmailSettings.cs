namespace PRM.Application.Settings;

/// <summary>
/// Gmail configuration for outgoing notifications (SMTP) and optional inbox checks (IMAP).
/// Use a Google App Password — not your normal Gmail password.
/// </summary>
public class EmailSettings
{
    public const string SectionName = "EmailSettings";

    public bool Enabled { get; set; }

    /// <summary>Full Gmail address, e.g. you@gmail.com</summary>
    public string GmailAddress { get; set; } = string.Empty;

    /// <summary>16-character Google App Password (spaces optional).</summary>
    public string AppPassword { get; set; } = string.Empty;

    public string FromName { get; set; } = "PRM System";

    // ── Outgoing (SMTP) — used to send PRM notifications ─────────
    public string SmtpHost { get; set; } = "smtp.gmail.com";
    public int SmtpPort { get; set; } = 587;
    public bool SmtpUseStartTls { get; set; } = true;

    // ── Incoming (IMAP) — optional inbox verification ──────────────
    public bool EnableInboxVerification { get; set; } = true;
    public string ImapHost { get; set; } = "imap.gmail.com";
    public int ImapPort { get; set; } = 993;

    public string NormalizedAppPassword => AppPassword.Replace(" ", string.Empty);

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(GmailAddress) && !string.IsNullOrWhiteSpace(AppPassword);
}
