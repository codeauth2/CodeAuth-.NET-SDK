# CodeAuth .NET SDK
[![NuGet](https://img.shields.io/nuget/v/stripe.net.svg)](https://www.nuget.org/packages/CodeAuthSDK/)

Offical CodeAuth SDK. For more info, check the docs on our [official website](https://docs.codeauth.com).

## Installation
From within Visual Studio:
1. Open the Solution Explorer.
2. Right-click on a project within your solution.
3. Click on _Manage NuGet Packages..._
4. Click on the _Browse_ tab and search for "CodeAuthSDK".
5. Click on the CodeAuthSDK package, select the appropriate version in the
   right-tab and click _Install_.

**OR**

Using the [.NET Core command-line interface (CLI) tools](https://docs.microsoft.com/en-us/dotnet/core/tools/)]
```sh
dotnet add package CodeAuthSDK
```

## Basic Usage

### Initialize CodeAuth SDK
```csharp
using CodeAuthSDK;
CodeAuth.Initialize("<your project API endpoint>",  "<your project ID>");
```

### Signin / Email
Begins the sign in or register flow by sending the user a one time code via email.
```csharp
var result = await CodeAuth.SignInEmail("<user email>");
switch (result.error)  
{  
	case "bad_json":  break;  
	case "project_not_found": break;
	case "bad_ip_address": break;
	case "rate_limit_reached": break;
	case "bad_email": break;
	case "code_request_interval_reached": break;
	case "code_hourly_limit_reached": break;
	case "email_provider_error": break;
	case "internal_error": break;
	case "connection_error": break; // sdk failed to connect to api server
}
```

### Signin / Email Verify
Checks if the one time code matches in order to create a session token.
```csharp
var result = await CodeAuth.SignInEmailVerify("<user email>", "<one time code>");
switch (result.error)
{
	case "bad_json": break;
	case "project_not_found": break;
	case "bad_ip_address": break;
	case "rate_limit_reached": break;
	case "bad_email": break;
	case "bad_code": break;
	case "internal_error": break;
	case "connection_error": break; // sdk failed to connect to api server
}
Console.WriteLine(result.session_token);
Console.WriteLine(result.email);
Console.WriteLine(result.expiration);
Console.WriteLine(result.refresh_left);
```

### Signin / Social
Begins the sign in or register flow by allowing users to sign in through a social OAuth2 link.
```csharp
var result = await CodeAuth.SignInSocial("<social type>");
switch (result.error)
{
	case "bad_json": break;
	case "project_not_found": break;
	case "bad_ip_address": break;
	case "rate_limit_reached": break;
	case "bad_social_type": break;
	case "internal_error": break;
	case "connection_error": break; // sdk failed to connect to api server
}
Console.WriteLine(result.signin_url);
```

### Signin / Social Verify
This is the next step after the user signs in with their social account. This request checks the authorization code given by the social media company in order to create a session token.
```csharp
var result = await CodeAuth.SignInSocialVerify("<social type>", "<authorization code>");
switch (result.error)
{
	case "bad_json": break;
	case "project_not_found": break;
	case "bad_ip_address": break;
	case "rate_limit_reached": break;
	case "bad_social_type": break;
	case "bad_authorization_code": break;
	case "internal_error": break;
	case "connection_error": break; // sdk failed to connect to api server
}
Console.WriteLine(result.session_token);
Console.WriteLine(result.email);
Console.WriteLine(result.expiration);
Console.WriteLine(result.refresh_left);
```

### Session / Info
Gets the information associated with a session token.
```csharp
var result = await CodeAuth.SessionInfo("<session_token>");
switch (result.error)
{
	case "bad_json": break;
	case "project_not_found": break;
	case "bad_ip_address": break;
	case "rate_limit_reached": break;
	case "bad_session_token": break;
	case "internal_error": break;
	case "connection_error": break; // sdk failed to connect to api server
}
Console.WriteLine(result.email);
Console.WriteLine(result.expiration);
Console.WriteLine(result.refresh_left);
```

### Session / Refresh
Create a new session token using existing session token.
```csharp
var result = await CodeAuth.SessionRefresh("<session_token>");
switch (result.error)
{
	case "bad_json": break;
	case "project_not_found": break;
	case "bad_ip_address": break;
	case "rate_limit_reached": break;
	case "bad_session_token": break;
	case "out_of_refresh": break;
	case "internal_error": break;
	case "connection_error": break; // sdk failed to connect to api server
}
Console.WriteLine(result.session_token);
Console.WriteLine(result.email);
Console.WriteLine(result.expiration);
Console.WriteLine(result.refresh_left);
```

### Session / Invalidate
Invalidate a session token. By doing so, the session token can no longer be used for any api call.
```csharp
var result = await CodeAuth.SessionInvalidate("<session_token>", "<invalidate_type>");
switch (result.error)
{
	case "bad_json": break;
	case "project_not_found": break;
	case "bad_ip_address": break;
	case "rate_limit_reached": break;
	case "bad_session_token": break;
	case "bad_invalidate_type": break;
	case "internal_error": break;
	case "connection_error": break; // sdk failed to connect to api server 
}
```

