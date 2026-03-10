namespace MirrorDeck.WinUI.Views;

public enum TrayActionKind
{
	Default,
	Start,
	Stop,
	Restart
}

public sealed record TrayMenuEntry(
	uint Id,
	string Label,
	bool IsEnabled = true,
	bool IsSeparator = false,
	bool IsActive = false,
	TrayActionKind ActionKind = TrayActionKind.Default);
