namespace CodigoActivo.Domain.Constants;

/// <summary>
/// Defines stable identifiers for built-in reference data.
/// </summary>
public static class SeedIds
{
    /// <summary>
    /// Defines stable identifiers for built-in user status types records.
    /// </summary>
    public static class UserStatusTypes
    {
        /// <summary>
        /// Stores the shared pending value.
        /// </summary>
        public static readonly Guid Pending = new("086e64b7-79e4-4b2d-a6c9-f69ff8243df1");

        /// <summary>
        /// Stores the shared active value.
        /// </summary>
        public static readonly Guid Active = new("766f114c-6168-4be5-89f2-bae2a7a919e4");

        /// <summary>
        /// Stores the shared blocked value.
        /// </summary>
        public static readonly Guid Blocked = new("37e9d1e6-1cf3-4c13-a1d8-41b86986d282");

        /// <summary>
        /// Stores the shared dependent value.
        /// </summary>
        public static readonly Guid Dependent = new("45a26e12-404b-43b8-b7a7-cdf5f0fc1c4d");
    }

    /// <summary>
    /// Defines stable identifiers for built-in user types records.
    /// </summary>
    public static class UserTypes
    {
        /// <summary>
        /// Stores the shared member value.
        /// </summary>
        public static readonly Guid Member = new("b0df7ac6-1312-412f-9c2a-88e6cdfb6e1c");

        /// <summary>
        /// Stores the shared sponsor value.
        /// </summary>
        public static readonly Guid Sponsor = new("8e0b7dc4-59d3-4c3b-9a71-4f25c6b0de88");

        /// <summary>
        /// Stores the shared participant value.
        /// </summary>
        public static readonly Guid Participant = new("1c038ae8-306f-4785-a5f5-b9c25e5cc4aa");
    }

    /// <summary>
    /// Defines stable identifiers for built-in activity role types records.
    /// </summary>
    public static class ActivityRoleTypes
    {
        /// <summary>
        /// Stores the shared leader value.
        /// </summary>
        public static readonly Guid Leader = new("5bd627de-831a-4169-874e-26a90550db9f");

        /// <summary>
        /// Stores the shared volunteer value.
        /// </summary>
        public static readonly Guid Volunteer = new("3b31564d-9879-434c-9152-5907db0c46fb");

        /// <summary>
        /// Stores the shared participant value.
        /// </summary>
        public static readonly Guid Participant = new("03a5613e-a2d2-42da-94d2-b6d63c0f01b5");
    }

    /// <summary>
    /// Defines stable identifiers for built-in assignment status types records.
    /// </summary>
    public static class AssignmentStatusTypes
    {
        /// <summary>
        /// Stores the shared requested value.
        /// </summary>
        public static readonly Guid Requested = new("3d717eeb-de06-44b8-b7df-2cc3e2ce5cb0");

        /// <summary>
        /// Stores the shared confirmed value.
        /// </summary>
        public static readonly Guid Confirmed = new("3c172c13-d238-4f0b-a61b-0a5ffc6a53ba");

        /// <summary>
        /// Stores the shared denied value.
        /// </summary>
        public static readonly Guid Denied = new("714c9041-5536-420a-8176-bf745957d80e");
    }

    /// <summary>
    /// Defines stable identifiers for built-in activity modality types records.
    /// </summary>
    public static class ActivityModalityTypes
    {
        /// <summary>
        /// Stores the shared presencial value.
        /// </summary>
        public static readonly Guid Presencial = new("3a7956c5-2346-4fc3-b3e8-10ecc07f1e1f");

        /// <summary>
        /// Stores the shared online value.
        /// </summary>
        public static readonly Guid Online = new("44b62b00-17f5-46c3-ac65-245fbd8e7db2");
    }

    /// <summary>
    /// Defines stable identifiers for built-in resource types records.
    /// </summary>
    public static class ResourceTypes
    {
        /// <summary>
        /// Stores the shared internal value.
        /// </summary>
        public static readonly Guid Internal = new("47833436-b131-4536-87a6-737a9680a423");

        /// <summary>
        /// Stores the shared external value.
        /// </summary>
        public static readonly Guid External = new("d4b28595-eed5-4728-ad87-8eaf9d8ce754");
    }
}
