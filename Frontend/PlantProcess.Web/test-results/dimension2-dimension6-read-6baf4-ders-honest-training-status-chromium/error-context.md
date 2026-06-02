# Instructions

- Following Playwright test failed.
- Explain why, be concise, respect Playwright best practices.
- Provide a snippet of code with the fix, if possible.

# Test info

- Name: dimension2-dimension6-readiness.spec.ts >> Dimension 6 ML readiness >> ML readiness page renders honest training status
- Location: e2e\dimension2-dimension6-readiness.spec.ts:44:3

# Error details

```
Error: PlantProcess IQ E2E login failed.

API base URL: http://localhost:5063
Smoke user: e2eadmin

Failures:
Attempt: Configured E2E real admin, PascalCase
URL: http://localhost:5063/auth/login
User: e2eadmin
HTTP: 500
Body: Npgsql.PostgresException (0x80004005): 23503: insert or update on table "auth_refresh_tokens" violates foreign key constraint "auth_refresh_tokens_user_id_fkey"

DETAIL: Detail redacted as it may contain sensitive data. Specify 'Include Error Detail' in the connection string to include this information.
   at Npgsql.Internal.NpgsqlConnector.ReadMessageLong(Boolean async, DataRowLoadingMode dataRowLoadingMode, Boolean readingNotifications, Boolean isReadingPrependedMessage)
   at System.Runtime.CompilerServices.PoolingAsyncValueTaskMethodBuilder`1.StateMachineBox`1.System.Threading.Tasks.Sources.IValueTaskSource<TResult>.GetResult(Int16 token)
   at Npgsql.NpgsqlDataReader.NextResult(Boolean async, Boolean isConsuming, CancellationToken cancellationToken)
   at Npgsql.NpgsqlDataReader.NextResult(Boolean async, Boolean isConsuming, CancellationToken cancellationToken)
   at Npgsql.NpgsqlCommand.ExecuteReader(Boolean async, CommandBehavior behavior, CancellationToken cancellationToken)
   at Npgsql.NpgsqlCommand.ExecuteReader(Boolean async, CommandBehavior behavior, CancellationToken cancellationToken)
   at Npgsql.NpgsqlCommand.ExecuteNonQuery(Boolean async, CancellationToken cancellationToken)
   at PlantProcess.Api.Security.AuthStore.StoreRefreshTokenAsync(Guid tenantId, Guid userId, String rawToken, DateTime expiresAtUtc, HttpContext httpContext, CancellationToken cancellationToken) in C:\Workspace\PlantProcess-IQ\Backend\PlantProcess.Api\Security\AuthStore.cs:line 137
   at PlantProcess.Api.Security.AuthStore.StoreRefreshTokenAsync(Guid tenantId, Guid userId, String rawToken, DateTime expiresAtUtc, HttpContext httpContext, CancellationToken cancellationToken) in C:\Workspace\PlantProcess-IQ\Backend\PlantProcess.Api\Security\AuthStore.cs:line 137
   at PlantProcess.Api.Security.AuthEndpoints.IssueRefreshCookieAsync(AppUserRecord user, AuthStore store, AuthOptions auth, HttpContext httpContext, CancellationToken cancellationToken) in C:\Workspace\PlantProcess-IQ\Backend\PlantProcess.Api\Security\AuthEndpoints.cs:line 307
   at PlantProcess.Api.Security.AuthEndpoints.LoginAsync(LoginRequest request, AuthStore authStore, IOptions`1 options, IWebHostEnvironment environment, HttpContext httpContext, IPlantEntitlementResolver entitlementResolver, ILoggerFactory loggerFactory, CancellationToken cancellationToken) in C:\Workspace\PlantProcess-IQ\Backend\PlantProcess.Api\Security\AuthEndpoints.cs:line 72
   at Microsoft.AspNetCore.Http.RequestDelegateFactory.ExecuteTaskResult[T](Task`1 task, HttpContext httpContext)
   at Microsoft.AspNetCore.Http.RequestDelegateFactory.<>c__DisplayClass101_2.<<HandleRequestBodyAndCompileRequestDelegateForJson>b__2>d.MoveNext()
--- End of stack trace from previous location ---
   at Swashbuckle.AspNetCore.SwaggerUI.SwaggerUIMiddleware.Invoke(HttpContext httpContext)
   at Swashbuckle.AspNetCore.Swagger.SwaggerMiddleware.Invoke(HttpContext httpContext, ISwaggerProvider swaggerProvider)
   at PlantProcess.Api.Middleware.AuditLogMiddleware.InvokeAsync(HttpContext context, IServiceProvider services) in C:\Workspace\PlantProcess-IQ\Backend\PlantProcess.Api\Middleware\AuditLogMiddleware.cs:line 70
   at PlantProcess.Api.Middleware.AuditLogMiddleware.InvokeAsync(HttpContext context, IServiceProvider services) in C:\Workspace\PlantProcess-IQ\Backend\PlantProcess.Api\Middleware\AuditLogMiddleware.cs:line 121
   at Microsoft.AspNetCore.Authorization.AuthorizationMiddleware.Invoke(HttpContext context)
   at PlantProcess.Api.Security.AccessControlMiddleware.InvokeAsync(HttpContext context, IPlantEntitlementResolver resolver) in C:\Workspace\PlantProcess-IQ\Backend\PlantProcess.Api\Security\PlantAccessControl.cs:line 243
   at Microsoft.AspNetCore.Authentication.AuthenticationMiddleware.Invoke(HttpContext context)
   at PlantProcess.Api.Middleware.RequestResponseLoggingMiddleware.InvokeAsync(HttpContext context) in C:\Workspace\PlantProcess-IQ\Backend\PlantProcess.Api\Middleware\RequestResponseLoggingMiddleware.cs:line 55
   at PlantProcess.Api.Middleware.CorrelationIdMiddleware.InvokeAsync(HttpContext context) in C:\Workspace\PlantProcess-IQ\Backend\PlantProcess.Api\Middleware\CorrelationIdMiddleware.cs:line 64
   at Microsoft.AspNetCore.Diagnostics.DeveloperExceptionPageMiddlewareImpl.Invoke(HttpContext context)
  Exception data:
    Severity: ERROR
    SqlState: 23503
    MessageText: insert or update on table "auth_refresh_tokens" violates foreign key constraint "auth_refresh_tokens_user_id_fkey"
    Detail: Detail redacted as it may contain sensitive data. Specify 'Include Error Detail' in the connection string to include this information.
    SchemaName: public
    TableName: auth_refresh_tokens
    ConstraintName: auth_refresh_tokens_user_id_fkey
    File: ri_triggers.c
    Line: 2619
    Routine: ri_ReportViolation

HEADERS
=======
Accept: application/json
Connection: keep-alive
Host: localhost:5063
User-Agent: Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/148.0.7778.96 Safari/537.36
Accept-Encoding: gzip,deflate,br
Content-Type: application/json
Content-Length: 66


---

Attempt: Configured E2E real admin, camelCase
URL: http://localhost:5063/auth/login
User: e2eadmin
HTTP: 500
Body: Npgsql.PostgresException (0x80004005): 23503: insert or update on table "auth_refresh_tokens" violates foreign key constraint "auth_refresh_tokens_user_id_fkey"

DETAIL: Detail redacted as it may contain sensitive data. Specify 'Include Error Detail' in the connection string to include this information.
   at Npgsql.Internal.NpgsqlConnector.ReadMessageLong(Boolean async, DataRowLoadingMode dataRowLoadingMode, Boolean readingNotifications, Boolean isReadingPrependedMessage)
   at System.Runtime.CompilerServices.PoolingAsyncValueTaskMethodBuilder`1.StateMachineBox`1.System.Threading.Tasks.Sources.IValueTaskSource<TResult>.GetResult(Int16 token)
   at Npgsql.NpgsqlDataReader.NextResult(Boolean async, Boolean isConsuming, CancellationToken cancellationToken)
   at Npgsql.NpgsqlDataReader.NextResult(Boolean async, Boolean isConsuming, CancellationToken cancellationToken)
   at Npgsql.NpgsqlCommand.ExecuteReader(Boolean async, CommandBehavior behavior, CancellationToken cancellationToken)
   at Npgsql.NpgsqlCommand.ExecuteReader(Boolean async, CommandBehavior behavior, CancellationToken cancellationToken)
   at Npgsql.NpgsqlCommand.ExecuteNonQuery(Boolean async, CancellationToken cancellationToken)
   at PlantProcess.Api.Security.AuthStore.StoreRefreshTokenAsync(Guid tenantId, Guid userId, String rawToken, DateTime expiresAtUtc, HttpContext httpContext, CancellationToken cancellationToken) in C:\Workspace\PlantProcess-IQ\Backend\PlantProcess.Api\Security\AuthStore.cs:line 137
   at PlantProcess.Api.Security.AuthStore.StoreRefreshTokenAsync(Guid tenantId, Guid userId, String rawToken, DateTime expiresAtUtc, HttpContext httpContext, CancellationToken cancellationToken) in C:\Workspace\PlantProcess-IQ\Backend\PlantProcess.Api\Security\AuthStore.cs:line 137
   at PlantProcess.Api.Security.AuthEndpoints.IssueRefreshCookieAsync(AppUserRecord user, AuthStore store, AuthOptions auth, HttpContext httpContext, CancellationToken cancellationToken) in C:\Workspace\PlantProcess-IQ\Backend\PlantProcess.Api\Security\AuthEndpoints.cs:line 307
   at PlantProcess.Api.Security.AuthEndpoints.LoginAsync(LoginRequest request, AuthStore authStore, IOptions`1 options, IWebHostEnvironment environment, HttpContext httpContext, IPlantEntitlementResolver entitlementResolver, ILoggerFactory loggerFactory, CancellationToken cancellationToken) in C:\Workspace\PlantProcess-IQ\Backend\PlantProcess.Api\Security\AuthEndpoints.cs:line 72
   at Microsoft.AspNetCore.Http.RequestDelegateFactory.ExecuteTaskResult[T](Task`1 task, HttpContext httpContext)
   at Microsoft.AspNetCore.Http.RequestDelegateFactory.<>c__DisplayClass101_2.<<HandleRequestBodyAndCompileRequestDelegateForJson>b__2>d.MoveNext()
--- End of stack trace from previous location ---
   at Swashbuckle.AspNetCore.SwaggerUI.SwaggerUIMiddleware.Invoke(HttpContext httpContext)
   at Swashbuckle.AspNetCore.Swagger.SwaggerMiddleware.Invoke(HttpContext httpContext, ISwaggerProvider swaggerProvider)
   at PlantProcess.Api.Middleware.AuditLogMiddleware.InvokeAsync(HttpContext context, IServiceProvider services) in C:\Workspace\PlantProcess-IQ\Backend\PlantProcess.Api\Middleware\AuditLogMiddleware.cs:line 70
   at PlantProcess.Api.Middleware.AuditLogMiddleware.InvokeAsync(HttpContext context, IServiceProvider services) in C:\Workspace\PlantProcess-IQ\Backend\PlantProcess.Api\Middleware\AuditLogMiddleware.cs:line 121
   at Microsoft.AspNetCore.Authorization.AuthorizationMiddleware.Invoke(HttpContext context)
   at PlantProcess.Api.Security.AccessControlMiddleware.InvokeAsync(HttpContext context, IPlantEntitlementResolver resolver) in C:\Workspace\PlantProcess-IQ\Backend\PlantProcess.Api\Security\PlantAccessControl.cs:line 243
   at Microsoft.AspNetCore.Authentication.AuthenticationMiddleware.Invoke(HttpContext context)
   at PlantProcess.Api.Middleware.RequestResponseLoggingMiddleware.InvokeAsync(HttpContext context) in C:\Workspace\PlantProcess-IQ\Backend\PlantProcess.Api\Middleware\RequestResponseLoggingMiddleware.cs:line 55
   at PlantProcess.Api.Middleware.CorrelationIdMiddleware.InvokeAsync(HttpContext context) in C:\Workspace\PlantProcess-IQ\Backend\PlantProcess.Api\Middleware\CorrelationIdMiddleware.cs:line 64
   at Microsoft.AspNetCore.Diagnostics.DeveloperExceptionPageMiddlewareImpl.Invoke(HttpContext context)
  Exception data:
    Severity: ERROR
    SqlState: 23503
    MessageText: insert or update on table "auth_refresh_tokens" violates foreign key constraint "auth_refresh_tokens_user_id_fkey"
    Detail: Detail redacted as it may contain sensitive data. Specify 'Include Error Detail' in the connection string to include this information.
    SchemaName: public
    TableName: auth_refresh_tokens
    ConstraintName: auth_refresh_tokens_user_id_fkey
    File: ri_triggers.c
    Line: 2619
    Routine: ri_ReportViolation

HEADERS
=======
Accept: application/json
Connection: keep-alive
Host: localhost:5063
User-Agent: Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/148.0.7778.96 Safari/537.36
Accept-Encoding: gzip,deflate,br
Content-Type: application/json
Content-Length: 66


Most likely fix:
1. Stop old backend/frontend processes on ports 5063 and 5173.
2. Let Playwright start both servers from playwright.config.ts.
3. Ensure E2E uses a real admin user, not bootstrap admin:
   legacy hardcoded E2E credentials
```

# Test source

```ts
  23  |   "e2eadmin";
  24  | 
  25  | export const smokePassword =
  26  |   process.env.PPIQ_SMOKE_PASSWORD ||
  27  |   process.env.VITE_SMOKE_PASSWORD ||
  28  |   "";
  29  | 
  30  | type LoginPayload = {
  31  |   label: string;
  32  |   data: Record<string, string>;
  33  | };
  34  | 
  35  | function normalizeApiUrl(path: string): string {
  36  |   if (path.startsWith("http://") || path.startsWith("https://")) {
  37  |     return path;
  38  |   }
  39  | 
  40  |   return `${apiBaseUrl}${path.startsWith("/") ? path : `/${path}`}`;
  41  | }
  42  | 
  43  | async function safeText(response: { text: () => Promise<string> }) {
  44  |   try {
  45  |     return await response.text();
  46  |   } catch {
  47  |     return "<unable to read response body>";
  48  |   }
  49  | }
  50  | 
  51  | export async function login(request: APIRequestContext): Promise<string> {
  52  |   const loginUrl = `${apiBaseUrl}/auth/login`;
  53  | 
  54  |   const attempts: LoginPayload[] = [
  55  |     {
  56  |       label: "Configured E2E real admin, PascalCase",
  57  |       data: {
  58  |         UserName: smokeUserName,
  59  |         Password: smokePassword,
  60  |       },
  61  |     },
  62  |     {
  63  |       label: "Configured E2E real admin, camelCase",
  64  |       data: {
  65  |         userName: smokeUserName,
  66  |         password: smokePassword,
  67  |       },
  68  |     },
  69  |   ];
  70  | 
  71  |   const failures: string[] = [];
  72  | 
  73  |   for (const attempt of attempts) {
  74  |     const response = await request.post(loginUrl, {
  75  |       data: attempt.data,
  76  |       headers: {
  77  |         Accept: "application/json",
  78  |         "Content-Type": "application/json",
  79  |       },
  80  |     });
  81  | 
  82  |     if (!response.ok()) {
  83  |       failures.push(
  84  |         [
  85  |           `Attempt: ${attempt.label}`,
  86  |           `URL: ${loginUrl}`,
  87  |           `User: ${attempt.data.UserName ?? attempt.data.userName}`,
  88  |           `HTTP: ${response.status()}`,
  89  |           `Body: ${await safeText(response)}`,
  90  |         ].join("\n")
  91  |       );
  92  | 
  93  |       continue;
  94  |     }
  95  | 
  96  |     const body = await response.json();
  97  | 
  98  |     const token =
  99  |       body.accessToken ||
  100 |       body.token ||
  101 |       body.jwt ||
  102 |       body.bearerToken ||
  103 |       body?.data?.accessToken ||
  104 |       body?.data?.token;
  105 | 
  106 |     expect(
  107 |       token,
  108 |       `Login response from ${loginUrl} must contain accessToken or token. Body:\n${JSON.stringify(
  109 |         body,
  110 |         null,
  111 |         2
  112 |       )}`
  113 |     ).toBeTruthy();
  114 | 
  115 |     expect(
  116 |       String(token).length,
  117 |       `Login token returned from ${loginUrl} is unexpectedly short.`
  118 |     ).toBeGreaterThan(20);
  119 | 
  120 |     return String(token);
  121 |   }
  122 | 
> 123 |   throw new Error(
      |         ^ Error: PlantProcess IQ E2E login failed.
  124 |     [
  125 |       "PlantProcess IQ E2E login failed.",
  126 |       "",
  127 |       `API base URL: ${apiBaseUrl}`,
  128 |       `Smoke user: ${smokeUserName}`,
  129 |       "",
  130 |       "Failures:",
  131 |       failures.join("\n\n---\n\n"),
  132 |       "",
  133 |       "Most likely fix:",
  134 |       "1. Stop old backend/frontend processes on ports 5063 and 5173.",
  135 |       "2. Let Playwright start both servers from playwright.config.ts.",
  136 |       "3. Ensure E2E uses a real admin user, not bootstrap admin:",
  137 |       "   legacy hardcoded E2E credentials",
  138 |     ].join("\n")
  139 |   );
  140 | }
  141 | 
  142 | export async function authenticatedGet(
  143 |   request: APIRequestContext,
  144 |   url: string
  145 | ) {
  146 |   const token = await login(request);
  147 | 
  148 |   return request.get(normalizeApiUrl(url), {
  149 |     headers: {
  150 |       Accept: "application/json",
  151 |       Authorization: `Bearer ${token}`,
  152 |     },
  153 |   });
  154 | }
  155 | 
  156 | export async function authenticatedPost<TBody = unknown>(
  157 |   request: APIRequestContext,
  158 |   url: string,
  159 |   body: TBody
  160 | ) {
  161 |   const token = await login(request);
  162 | 
  163 |   return request.post(normalizeApiUrl(url), {
  164 |     data: body,
  165 |     headers: {
  166 |       Accept: "application/json",
  167 |       Authorization: `Bearer ${token}`,
  168 |     },
  169 |   });
  170 | }
  171 | 
  172 | export async function authHeaders(
  173 |   request: APIRequestContext
  174 | ): Promise<Record<string, string>> {
  175 |   const token = await login(request);
  176 | 
  177 |   return {
  178 |     Accept: "application/json",
  179 |     Authorization: `Bearer ${token}`,
  180 |   };
  181 | }
```