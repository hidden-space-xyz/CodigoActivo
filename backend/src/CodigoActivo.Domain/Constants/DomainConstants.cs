namespace CodigoActivo.Domain.Constants;

public static class SeedIds
{
    public static class UserStatusTypes
    {
        public static readonly Guid Pending = new("086e64b7-79e4-4b2d-a6c9-f69ff8243df1");
        public static readonly Guid Active = new("766f114c-6168-4be5-89f2-bae2a7a919e4");
        public static readonly Guid Blocked = new("37e9d1e6-1cf3-4c13-a1d8-41b86986d282");

        public static readonly Guid Dependent = new("45a26e12-404b-43b8-b7a7-cdf5f0fc1c4d");
    }

    public static class UserTypes
    {
        public static readonly Guid Member = new("b0df7ac6-1312-412f-9c2a-88e6cdfb6e1c");
        public static readonly Guid Volunteer = new("c26c7755-b5db-42fe-b349-e83a84481fea");
        public static readonly Guid Participant = new("1c038ae8-306f-4785-a5f5-b9c25e5cc4aa");
    }

    public static class ActivityRoleTypes
    {
        public static readonly Guid Leader = new("5bd627de-831a-4169-874e-26a90550db9f");
        public static readonly Guid Helper = new("3b31564d-9879-434c-9152-5907db0c46fb");
        public static readonly Guid Participant = new("03a5613e-a2d2-42da-94d2-b6d63c0f01b5");
    }

    public static class AssignmentStatusTypes
    {
        public static readonly Guid Requested = new("3d717eeb-de06-44b8-b7df-2cc3e2ce5cb0");
        public static readonly Guid Confirmed = new("3c172c13-d238-4f0b-a61b-0a5ffc6a53ba");
        public static readonly Guid Denied = new("714c9041-5536-420a-8176-bf745957d80e");
    }

    public static class ActivityModalityTypes
    {
        public static readonly Guid Presencial = new("3a7956c5-2346-4fc3-b3e8-10ecc07f1e1f");
        public static readonly Guid Online = new("44b62b00-17f5-46c3-ac65-245fbd8e7db2");
    }
}
