v1.2.1
- New/Renew/Reissue requests with a prior certificate are resolved from the order date: reissued under the active order outside the renewal window (RenewalWindowDays, default 30), renewed inside it, ordered new once expired, supporting shorter-lived (~200 day) certificates
- Added FlowLogger step diagrams to the Trace log for Enroll, Synchronize, Revoke, GetSingleRecord and Initialize
- Removed the 'cannot renew an expired cert' failure; prior certificate serial numbers now match with or without the leading 00 sign byte
- Hardening: validate required enrollment parameters and prior certificate before calling CSC, clearer error messages, null response checks, culture-safe date parsing, per-certificate error isolation during sync, original exceptions preserved
- Support CSC's current certificate type names (OV, OV Wildcard, OV Multiple Names, EV, DV, DV Wildcard, DV Multiple Names) and the new EV Multiple Names, OV Wildcard Multiple Names and DV Wildcard Multiple Names types; legacy product names keep working. SANs are now sent for all Multiple Names types and EV details for both EV types
- Removed the bundled template files, product CSV and template install script; templates are now set up by Keyfactor Support
- Product names with commas (for example 'OV, Multiple Names') are translated to hyphens ('OV - Multiple Names') for Active Directory template names, both when syncing and when matching templates on enrollment; sample config and README updated
- Fix reissue/renewal failing to read CSC's response when the price total is null (free reissue under an active order)
- A certificate that CSC Global does not have yet (for example an order waiting for approval) is reported as pending instead of an error, and comes down with a later sync
- Clearer message when CSC Global rejects a request because an order is already in progress for the certificate, and the flow log now shows a CSC rejection as a failed step

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

