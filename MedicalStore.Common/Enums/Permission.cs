namespace MedicalStore.Common.Enums
{
    [Flags]
    public enum Permission
    {
        None = 0,
        ViewDashboard = 1,
        ManageProducts = 2,
        ManagePurchases = 4,
        ManageCustomers = 8,
        UsePOS = 16,
        ManageReturns = 32,
        ViewReports = 64,
        ViewFinancials = 128,
        ManageUsers = 256,
        SystemBackup = 512,
        ManageSettings = 1024,

        // Role presets
        CashierDefault = ViewDashboard | UsePOS | ManageCustomers | ManageReturns,
        ManagerDefault = ViewDashboard | ManageProducts | ManageCustomers | UsePOS | ManageReturns | ViewReports | ManagePurchases,
        AdminAll = ViewDashboard | ManageProducts | ManagePurchases | ManageCustomers | UsePOS | ManageReturns | ViewReports | ViewFinancials | ManageUsers | SystemBackup | ManageSettings
    }
}
