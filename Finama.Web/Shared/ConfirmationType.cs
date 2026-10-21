namespace Finama.Web;

/// <summary>Gravité d'une confirmation : Danger pour tout ce qui est destructeur ou irréversible.</summary>
public enum ConfirmationType
{
    Question,
    Avertissement,
    Danger
}
