namespace AquaPlan.Domain.Enums;

public static class PermissionName
{
    // Requérant permissions
    public const string CreateOrder = "CreateOrder";
    public const string EditOwnOrder = "EditOwnOrder";
    public const string ViewOwnOrder = "ViewOwnOrder";
    public const string AssignPreleveur = "AssignPreleveur";
    public const string ViewResults = "ViewResults";

    // Préleveur permissions
    public const string ViewAssignedOrder = "ViewAssignedOrder";
    public const string EditPrelevement = "EditPrelevement";
    public const string ValidateOrder = "ValidateOrder";
    public const string CreateUnplannedOrder = "CreateUnplannedOrder";

    // Administrator permissions
    public const string ManageUsers = "ManageUsers";
    public const string ManageRoles = "ManageRoles";
    public const string ManageDistributors = "ManageDistributors";
    public const string ManageLdp = "ManageLdp";
    public const string ManageAnalysisPrograms = "ManageAnalysisPrograms";
    public const string ViewAllOrders = "ViewAllOrders";
    public const string ViewAllResults = "ViewAllResults";
    public const string AdministerSystem = "AdministerSystem";
}
