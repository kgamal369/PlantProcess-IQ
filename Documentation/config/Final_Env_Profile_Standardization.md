# PlantProcess IQ - Final Env/Profile Standardization

Generated at: 2026-06-04 13:10:34

Rule: switching machine means choosing a profile, not editing committed config files.

## Gate Summary

| Status | Count |
|---|---:|
| MISSING | 2 |
| OK | 12 |

## Profile Key Summary

| Status | Count |
|---|---:|
| MISSING | 30 |
| OK | 51 |
| WARN | 4 |

## Gates

| Gate | Status | Evidence | Action |
|---|---|---|---|
| Required env key contract | MISSING | 30 required profile key(s) missing. | Repair profile templates. |
| Website env generated | MISSING | Website/.env.local not generated. | Repair use-profile output. |
| Apply local profile | OK | scripts/env/use-profile.ps1 -Profile local executed. | No action. |
| Final green wrapper | OK | scripts/test/validate-current-green-final.ps1 created. | Use this when you need visible final success. |
| Frontend env generated | OK | Frontend/PlantProcess.Web/.env.local exists after profile apply. | No action. |
| frontend env local ignored | OK | Frontend .env.local is covered by .gitignore. | No action. |
| local.env ignored | OK | env/profiles/local.env is covered by .gitignore. | No action. |
| Pack 2C high secret cleanup | OK | Latest Pack2C scan has zero HIGH findings. | No action. |
| Profile exists: customer-template | OK | C:\Workspace\PlantProcess-IQ\env\profiles\customer-template.env.example | No action. |
| Profile exists: local | OK | C:\Workspace\PlantProcess-IQ\env\profiles\local.env | No action. |
| Profile exists: local-example | OK | C:\Workspace\PlantProcess-IQ\env\profiles\local.env.example | No action. |
| Profile exists: server-docker-example | OK | C:\Workspace\PlantProcess-IQ\env\profiles\server-docker.env.example | No action. |
| Profile exists: test-example | OK | C:\Workspace\PlantProcess-IQ\env\profiles\test.env.example | No action. |
| website env local ignored | OK | Website .env.local is covered by .gitignore. | No action. |
