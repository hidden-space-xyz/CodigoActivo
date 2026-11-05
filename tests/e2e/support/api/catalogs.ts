/**
 * Stable identifiers of the catalog rows (`CatalogIds` in the backend), which the wire contract
 * accepts and returns for user statuses and types, roles, signup statuses, modalities and resource
 * types.
 */
export const CATALOG = {
  userStatuses: {
    pending: '086e64b7-79e4-4b2d-a6c9-f69ff8243df1',
    active: '766f114c-6168-4be5-89f2-bae2a7a919e4',
    blocked: '37e9d1e6-1cf3-4c13-a1d8-41b86986d282',
    dependent: '45a26e12-404b-43b8-b7a7-cdf5f0fc1c4d',
  },
  userTypes: {
    member: 'b0df7ac6-1312-412f-9c2a-88e6cdfb6e1c',
    sponsor: '8e0b7dc4-59d3-4c3b-9a71-4f25c6b0de88',
    participant: '1c038ae8-306f-4785-a5f5-b9c25e5cc4aa',
  },
  roles: {
    leader: '5bd627de-831a-4169-874e-26a90550db9f',
    volunteer: '3b31564d-9879-434c-9152-5907db0c46fb',
    participant: '03a5613e-a2d2-42da-94d2-b6d63c0f01b5',
  },
  signupStatuses: {
    requested: '3d717eeb-de06-44b8-b7df-2cc3e2ce5cb0',
    confirmed: '3c172c13-d238-4f0b-a61b-0a5ffc6a53ba',
    denied: '714c9041-5536-420a-8176-bf745957d80e',
  },
  modalities: {
    onSite: '3a7956c5-2346-4fc3-b3e8-10ecc07f1e1f',
    online: '44b62b00-17f5-46c3-ac65-245fbd8e7db2',
  },
  resourceTypes: {
    internal: '47833436-b131-4536-87a6-737a9680a423',
    external: 'd4b28595-eed5-4728-ad87-8eaf9d8ce754',
  },
} as const

/** A type of user account: member, sponsor or participant. */
export type UserTypeName = keyof typeof CATALOG.userTypes

/** A role a person takes in an activity. */
export type RoleName = keyof typeof CATALOG.roles

/** The state of a signup. */
export type SignupStatusName = keyof typeof CATALOG.signupStatuses
