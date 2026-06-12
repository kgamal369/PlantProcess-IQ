// PPIQ-T08: single canonical tier model. Phase10LicenseTier survives as a compile-time
// alias only; all logic runs on LicenseTier { Light=1, Pro=2, ProPlus=3, Enterprise=4 }.
global using Phase10LicenseTier = PlantProcess.Application.Licensing.Contracts.LicenseTier;