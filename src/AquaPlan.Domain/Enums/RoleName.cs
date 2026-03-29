namespace AquaPlan.Domain.Enums;

public static class RoleName
{
    public const string Requerant = "Requérant";
    public const string Preleveur = "Préleveur";
    public const string RequerantPreleveur = "Requérant-Préleveur";
    public const string Administrator = "Administrator";

    public static readonly string[] All =
    [
        Requerant,
        Preleveur,
        RequerantPreleveur,
        Administrator,
    ];
}
