using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace CodeAuthSDK
{
    /// <summary>
    /// CodeAuth SDK
    /// </summary>
    public static class CodeAuth
    {
        static string? Endpoint;
        static string? ProjectID;
        static bool UseCache;

        static bool HasInitialized;

        /// <summary>
        /// Initialize the CodeAuth SDK
        /// </summary>
        /// <param name="project_endpoint">The endpoint of your project. This can be found inside your project settings.</param>
        /// <param name="project_id">Your project ID. This can be found inside your project settings.</param>
        /// <param name="use_cache">Whether to use cache or not. Using cache can help speed up response time and mitigate some rate limits. This will automatically cache new session token (from '/signin/emailverify', 'signin/socialverify', 'session/info', 'session/refresh') and automatically delete cache when it is invalidated (from 'session/refresh', 'session/invalidate').</param>
        /// <param name="cache_duration">How long the cache should last. At least 15 seconds required to effectively mitigate most rate limits. Check docs for more info.</param>
        public static void Initialize(string project_endpoint, string project_id, bool use_cache = true, int cache_duration = 30)
        {
            if (HasInitialized) throw new Exception("CodeAuth has already been initialized");
            HasInitialized = true;

            Endpoint = project_endpoint;
            ProjectID = project_id;
            UseCache = use_cache;

            if (use_cache) StartBackgroundCache(cache_duration);
        }



        // -------
        // Background worker for session cache
        // -------
        static ConcurrentDictionary<string, SessionCacheData> session_cache = new ConcurrentDictionary<string, SessionCacheData>();
        class SessionCacheData
        {
            public string? email;
            public long expiration;
            public int refresh_left;
        }
        static void StartBackgroundCache(int cache_duration)
        {
            // run background task
            Task.Run(async () =>
            {
                // clear cache every x seconds
                using var timer = new PeriodicTimer(TimeSpan.FromSeconds(cache_duration));
                while (await timer.WaitForNextTickAsync())
                {
                    session_cache.Clear();
                }
            });
        }

        // -------
        // Makes sure that the CodeAuth SDK has been initialized
        // -------
        static void EnsureInitialized()
        {
            if (!HasInitialized) throw new Exception("CodeAuth has not been initialized");
        }

        // -------
        // Create api request and call server
        // -------
        static async Task<HttpResponseMessage> CallApiRequest(string path, object data)
        {
            // create http client
            using var client = new HttpClient();

            // return result
            return await client.PostAsync(
                "https://" + Endpoint + path,
                new StringContent(JsonSerializer.Serialize(data), Encoding.UTF8, "application/json")
            );
        }




        /// <summary>
        /// Begins the sign in or register flow by sending the user a one time code via email
        /// </summary>
        /// <param name="email">The email of the user you are trying to sign in/up. Email must be between 1 and 64 characters long. The email must also only contain letter, number, dot (not first, last, or consecutive), underscore (not first or last) and/or hyphen (not first or last).</param>
        /// <returns></returns>
        public static async Task<SignInEmailResult> SignInEmail(string email)
        {
            // make sure CodeAuth SDK has been initialized
            EnsureInitialized();

            // exception mitigation
            try
            {
                // call server and get response
                var response = await CallApiRequest("/signin/email", new { project_id = ProjectID, email = email });

                // handle OK (200) status: it should return nothing
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    return new SignInEmailResult { error = "no_error" };
                }
                // handle BAD REQUEST (400): it should return error property
                else if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                {
                    using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                    return new SignInEmailResult { error = doc.RootElement.GetProperty("error").GetString() };
                }
                // handle other status code: it should not have other status code
                else return new SignInEmailResult { error = "connection_error" };
            }
            catch { return new SignInEmailResult { error = "connection_error" }; }

        }

        /// <summary>
        /// Checks if the one time code matches in order to create a session token.
        /// </summary>
        /// <param name="email">The email of the user you are trying to sign in/up. Email must be between 1 and 64 characters long. The email must also only contain letter, number, dot (not first, last, or consecutive), underscore(not first or last) and/or hyphen(not first or last).</param>
        /// <param name="code">The one time code that was sent to the email.</param>
        /// <returns></returns>
        public static async Task<SignInEmailVerifyResult> SignInEmailVerify(string email, string code)
        {
            // make sure CodeAuth SDK has been initialized
            EnsureInitialized();

            // exception mitigation
            try
            {
                // call server and get response
                var response = await CallApiRequest("/signin/emailverify", new { project_id = ProjectID, email = email, code = code });

                // handle OK (200) status: the response should have information about session token
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    // get the properties from json: session_token, email, expiration, refresh_left
                    using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                    var root = doc.RootElement;
                    var response_session_token = root.GetProperty("session_token").GetString();
                    var response_email = root.GetProperty("email").GetString();
                    var response_expiration = root.GetProperty("expiration").GetInt64();
                    var response_refresh_left = root.GetProperty("refresh_left").GetInt32();

                    // save to cache if enabled
                    if (UseCache)
                    {
                        session_cache.GetOrAdd(response_session_token, new SessionCacheData { email = response_email, expiration = response_expiration, refresh_left = response_refresh_left });
                    }

                    // returns signin email verify result: session token information
                    return new SignInEmailVerifyResult
                    {
                        session_token = response_session_token,
                        email = response_email,
                        expiration = response_expiration,
                        refresh_left = response_refresh_left,
                        error = "no_error"
                    };
                }
                // handle BAD REQUEST (400): it should return error property
                else if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                {
                    // get the properties from json: error
                    using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

                    // returns signin email verify result: error
                    return new SignInEmailVerifyResult { error = doc.RootElement.GetProperty("error").GetString() };
                }
                // handle other status code: it should not have other status code
                else return new SignInEmailVerifyResult { error = "connection_error" };
            }
            catch { return new SignInEmailVerifyResult { error = "connection_error" }; }
        }


        /// <summary>
        /// Begins the sign in or register flow by allowing users to sign in through a social OAuth2 link.
        /// </summary>
        /// <param name="social_type">The type of social OAuth2 url you are trying to create. Possible social types: "google", "microsoft", "apple"</param>
        /// <returns></returns>
        public static async Task<SignInSocialResult> SignInSocial(string social_type)
        {
            // make sure CodeAuth SDK has been initialized
            EnsureInitialized();

            // exception mitigation
            try
            {
                // call server and get response
                var response = await CallApiRequest("/signin/social", new { project_id = ProjectID, social_type = social_type });

                // handle OK (200) status: the response should have information about session token
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    // get the properties from json: signin_url
                    using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                    var root = doc.RootElement;
                    var response_signin_url = root.GetProperty("signin_url").GetString();

                    // return signin social result: signin url
                    return new SignInSocialResult { signin_url = response_signin_url, error = "no_error" };
                }
                // handle BAD REQUEST (400): it should return error property
                else if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                {
                    // get the properties from json: error
                    using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

                    // returns signin social result: error
                    return new SignInSocialResult { error = doc.RootElement.GetProperty("error").GetString() };
                }
                // handle other status code: it should not have other status code
                else return new SignInSocialResult { error = "connection_error" };
            }
            catch { return new SignInSocialResult { error = "connection_error" }; }
        }


        /// <summary>
        /// This is the next step after the user signs in with their social account. This request checks the authorization code given by the social media company in order to create a session token.
        /// </summary>
        /// <param name="social_type">The type of social OAuth2 url you are trying to verify</param>
        /// <param name="authorization_code">The authorization code given by the social. Please read the doc for more info.</param>
        /// <returns></returns>
        public static async Task<SignInSocialVerifyResult> SignInSocialVerify(string social_type, string authorization_code)
        {
            // make sure CodeAuth SDK has been initialized
            EnsureInitialized();

            // exception mitigation
            try
            {
                // call server and get response
                var response = await CallApiRequest("/signin/socialverify", new { project_id = ProjectID, social_type = social_type, authorization_code = authorization_code });

                // handle OK (200) status: the response should have information about session token
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    // get the properties from json: session_token, email, expiration, refresh_left
                    using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                    var root = doc.RootElement;
                    var response_session_token = root.GetProperty("session_token").GetString();
                    var response_email = root.GetProperty("email").GetString();
                    var response_expiration = root.GetProperty("expiration").GetInt64();
                    var response_refresh_left = root.GetProperty("refresh_left").GetInt32();

                    // save to cache if enabled
                    if (UseCache)
                    {
                        session_cache.GetOrAdd(response_session_token, new SessionCacheData { email = response_email, expiration = response_expiration, refresh_left = response_refresh_left });
                    }

                    // return signin social verify result
                    return new SignInSocialVerifyResult
                    {
                        session_token = response_session_token,
                        email = response_email,
                        expiration = response_expiration,
                        refresh_left = response_refresh_left,
                        error = "no_error"
                    };
                }
                // handle BAD REQUEST (400): it should return error property
                else if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                {
                    // get the properties from json: error
                    using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

                    // returns signin social verify result: error
                    return new SignInSocialVerifyResult { error = doc.RootElement.GetProperty("error").GetString() };
                }
                // handle other status code: it should not have other status code
                else return new SignInSocialVerifyResult { error = "connection_error" };
            }
            catch { return new SignInSocialVerifyResult { error = "connection_error" }; }
        }



        /// <summary>
        /// Gets the information associated with a session token
        /// </summary>
        /// <param name="session_token">The session token you are trying to get information on</param>
        /// <returns></returns>
        public static async Task<SessionInfoResult> SessionInfo(string session_token)
        {
            // make sure CodeAuth SDK has been initialized
            EnsureInitialized();

            // trying geting from cache first if cache is enabled
            if (UseCache && session_cache.TryGetValue(session_token, out var cache_data))
            {
                // return session info result
                return new SessionInfoResult
                {
                    email = cache_data.email,
                    expiration = cache_data.expiration,
                    refresh_left = cache_data.refresh_left,
                    error = "no_error"
                };
            }

            // exception mitigation
            try
            {
                // call server and get response
                var response = await CallApiRequest("/session/info", new { project_id = ProjectID, session_token = session_token });

                // handle OK (200) status: the response should have information about session token
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    // get the properties from json: email, expiration, refresh_left
                    using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                    var root = doc.RootElement;
                    var response_email = root.GetProperty("email").GetString();
                    var response_expiration = root.GetProperty("expiration").GetInt64();
                    var response_refresh_left = root.GetProperty("refresh_left").GetInt32();

                    // save to cache if enabled
                    if (UseCache)
                    {
                        session_cache.GetOrAdd(session_token, new SessionCacheData { email = response_email, expiration = response_expiration, refresh_left = response_refresh_left });
                    }

                    // return session info result
                    return new SessionInfoResult
                    {
                        email = response_email,
                        expiration = response_expiration,
                        refresh_left = response_refresh_left,
                        error = "no_error"
                    };
                }
                // handle BAD REQUEST (400): it should return error property
                else if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                {
                    // get the properties from json: error
                    using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                    return new SessionInfoResult { error = doc.RootElement.GetProperty("error").GetString() };
                }
                // handle other status code: it should not have other status code
                else return new SessionInfoResult { error = "connection_error" };
            }
            catch { return new SessionInfoResult { error = "connection_error" }; }

        }


        /// <summary>
        /// Create a new session token using existing session token
        /// </summary>
        /// <param name="session_token">The session token you are trying to use to create a new token</param>
        /// <returns></returns>
        public static async Task<SessionRefreshResult> SessionRefresh(string session_token)
        {
            // make sure CodeAuth SDK has been initialized
            EnsureInitialized();

            // exception mitigation
            try
            {
                // call server and get response
                var response = await CallApiRequest("/session/refresh", new { project_id = ProjectID, session_token = session_token });

                // handle OK (200) status: the response should have information about session token
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                    var root = doc.RootElement;
                    var response_session_token = root.GetProperty("session_token").GetString();
                    var response_email = root.GetProperty("email").GetString();
                    var response_expiration = root.GetProperty("expiration").GetInt64();
                    var response_refresh_left = root.GetProperty("refresh_left").GetInt32();

                    // if cache is enabled, we need to delete the old session token cache (if it exist) and add the new token
                    if (UseCache)
                    {
                        session_cache.TryRemove(session_token, out var cache);
                        session_cache.GetOrAdd(response_session_token ?? string.Empty, new SessionCacheData { email = response_email, expiration = response_expiration, refresh_left = response_refresh_left });
                    }

                    // return session refresh result
                    return new SessionRefreshResult
                    {
                        session_token = response_session_token,
                        email = response_email,
                        expiration = response_expiration,
                        refresh_left = response_refresh_left,
                        error = "no_error"
                    };
                }
                // handle BAD REQUEST (400): it should return error property
                else if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                {
                    // get the properties from json: error
                    using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

                    // returns session refresh result: error
                    return new SessionRefreshResult { error = doc.RootElement.GetProperty("error").GetString() };
                }
                // handle other status code: it should not have other status code
                else return new SessionRefreshResult { error = "connection_error" };
            }
            catch { return new SessionRefreshResult { error = "connection_error" }; }

        }


        /// <summary>
        /// Invalidate a session token. By doing so, the session token can no longer be used for any api call.
        /// </summary>
        /// <param name="session_token">The session token you are trying to use to invalidate</param>
        /// <param name="invalidate_type">How to use the session token to invalidate. Possible invalidate types: "only_this", "all", "all_but_this"</param>
        /// <returns></returns>
        public static async Task<SessionInvalidateResult> SessionInvalidate(string session_token, string invalidate_type)
        {
            // make sure CodeAuth SDK has been initialized
            EnsureInitialized();

            // exception mitigation
            try
            {
                // call server and get response
                var response = await CallApiRequest("/session/invalidate", new { project_id = ProjectID, session_token = session_token, invalidate_type = invalidate_type });

                // handle OK (200) status: the response should have information about session token
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    // if cache is enabled, we need to delete the session token cache (if it exist)
                    if (UseCache) session_cache.TryRemove(session_token, out var cache);

                    // returns session invalidate result
                    return new SessionInvalidateResult { error = "no_error" };
                }
                // handle BAD REQUEST (400): it should return error property
                else if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                {
                    // get the properties from json: error
                    using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

                    // returns session invalidate result: error
                    return new SessionInvalidateResult { error = doc.RootElement.GetProperty("error").GetString() };
                }
                // handle other status code: it should not have other status code
                else return new SessionInvalidateResult { error = "connection_error" };
            }
            catch { return new SessionInvalidateResult { error = "connection_error" }; }
        }


    }

    /// <summary>
    /// Result of signin email
    /// </summary>
    public class SignInEmailResult
    {
        /// <summary>
        /// The error type. Possible error types for this response: BadJson, ProjectNotFound, BadIPAddress, RateLimitReached, BadEmail, CodeRequestIntervalReached, CodeHourlyLimitReached, InternalError
        /// </summary>
        public string? error;
    }
    /// <summary>
    /// Result of signin verify
    /// </summary>
    public class SignInEmailVerifyResult
    {
        /// <summary>
        /// The session token.
        /// </summary>
        public string? session_token;

        /// <summary>
        /// The email associated with the session token. The email is from the user's social account. Please note that Apple has a 'hide my email' feature that uses an alias to the real email address.
        /// </summary>
        public string? email;
        /// <summary>
        /// The unix timestamp when the session token will expire.
        /// </summary>
        public long expiration;
        /// <summary>
        /// Number of times this session token could still be refreshed.
        /// </summary>
        public int refresh_left;

        /// <summary>
        /// The error type. Possible error types for this response: BadJson, ProjectNotFound, BadIPAddress, RateLimitReached, BadSocialType, BadCode, InternalError
        /// </summary>
        public string? error;
    }
    /// <summary>
    /// Result of signin social
    /// </summary>
    public class SignInSocialResult
    {
        /// <summary>
        /// A social sign-in url that the user can use to sign into their social account.
        /// </summary>
        public string? signin_url;

        /// <summary>
        /// The error type. Possible error types for this response: BadJson, ProjectNotFound, BadIPAddress, RateLimitReached, BadSocialType, InternalError
        /// </summary>
        public string? error;
    }
    /// <summary>
    /// Result of signin social verify
    /// </summary>
    public class SignInSocialVerifyResult
    {
        /// <summary>
        /// The session token.
        /// </summary>
        public string? session_token;

        /// <summary>
        /// The email associated with the session token. The email is from the user's social account. Please note that Apple has a 'hide my email' feature that uses an alias to the real email address.
        /// </summary>
        public string? email;
        /// <summary>
        /// The unix timestamp when the session token will expire.
        /// </summary>
        public long expiration;
        /// <summary>
        /// Number of times this session token could still be refreshed.
        /// </summary>
        public int refresh_left;

        /// <summary>
        /// The error type. Possible error types for this response: BadJson, ProjectNotFound, BadIPAddress, RateLimitReached, BadSocialType, BadAuthorizationCode, InternalError
        /// </summary>
        public string? error;
    }
    /// <summary>
    /// Result of session info
    /// </summary>
    public class SessionInfoResult
    {
        /// <summary>
        /// The email associated with the session token.
        /// </summary>
        public string? email;
        /// <summary>
        /// The unix timestamp when the session token will expire.
        /// </summary>
        public long expiration;
        /// <summary>
        /// Number of times this session token could still be refreshed.
        /// </summary>
        public int refresh_left;

        /// <summary>
        /// The error type. Possible error types for this response: BadJson, ProjectNotFound, BadIPAddress, RateLimitReached, BadSessionToken, InternalError
        /// </summary>
        public string? error;
    }

    /// <summary>
    /// Result of session refresh
    /// </summary>
    public class SessionRefreshResult
    {
        /// <summary>
        /// The new session token.
        /// </summary>
        public string? session_token;

        /// <summary>
        /// The email associated with the session token.
        /// </summary>
        public string? email;
        /// <summary>
        /// The unix timestamp when the session token will expire.
        /// </summary>
        public long expiration;
        /// <summary>
        /// Number of times this session token could still be refreshed.
        /// </summary>
        public int refresh_left;

        /// <summary>
        /// The error type. Possible error types for this response: BadJson, ProjectNotFound, BadIPAddress, RateLimitReached, BadSessionToken, OutOfRefresh, InternalError
        /// </summary>
        public string? error;
    }

    /// <summary>
    /// Result of session invalidate
    /// </summary>
    public class SessionInvalidateResult
    {
        /// <summary>
        /// The error type. Possible error types for this response: BadJson, ProjectNotFound, BadIPAddress, RateLimitReached, BadSessionToken, InternalError
        /// </summary>
        public string? error;
    }
}
