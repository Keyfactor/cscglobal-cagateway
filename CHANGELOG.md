v1.2.1
- New/Renew/Reissue requests with a prior certificate are resolved from the order date: reissued under the active order outside the renewal window (RenewalWindowDays, default 30), renewed inside it, ordered new once expired, supporting shorter-lived (~200 day) certificates
- Added FlowLogger step diagrams to the Trace log for Enroll, Synchronize, Revoke, GetSingleRecord and Initialize
- Removed the 'cannot renew an expired cert' failure; prior certificate serial numbers now match with or without the leading 00 sign byte
- Hardening: validate required enrollment parameters and prior certificate before calling CSC, clearer error messages, null response checks, culture-safe date parsing, per-certificate error isolation during sync, original exceptions preserved

v1.2.0
- Incremental Sync Support with effectiveDate within last 5 days

v1.1.2
- Fix Renewal bug referencing the wrong REST Resource V1
  
v1.1.1
- Fix Revoke Serial Number Mismatch KF 10.1 and 22.1.0 GW combination
- Only Syncing and GetSingleRecord on End Entity Cert to prevent errors.

v1.1.0
- Add Support for CNAME Domain Validation 

v1.0.10
- Sync Issue where Sync only works after service restart fixed
- Sync Fix when errors occur in the CSC Api so next sync works
- Support for CSC TrustedSecure Domain Validated SSL (new CSC Template)
- Support for CSC TrustedSecure Domain Validated Wildcard SSL (new CSC Template)
- Support for CSC TrustedSecure Domain Validated UC Certificate (new CSC Template)
- Install support for new templates.  Setup only no code changed in Gateway.

v1.0.9
- Updated Readme to go from Powershell based install to Manual Install With More Detailed Instructions
- Removed unneeded config items
- Fixed Meta Data Sync Issue
- For Patch Installation for V1.0.9, see bottom of Readme File

