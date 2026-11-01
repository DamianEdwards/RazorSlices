using System.Globalization;
using System.Net;
using System.Net.Mime;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using RazorSlices.Samples.WebApp.Models;

namespace RazorSlices.Samples.WebApp.Tests;

public class HtmxTodoTests
{
    private const string TodoPath = "/htmx-todo";
    private const string TokenField = "__RequestVerificationToken";

    [Fact]
    public async Task TodoLifecycle_RendersPageAndFragmentsAndPersistsExplicitState()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        using var pageResponse = await client.GetAsync(TodoPath);

        Assert.Equal(HttpStatusCode.OK, pageResponse.StatusCode);
        Assert.Equal(MediaTypeNames.Text.Html, pageResponse.Content.Headers.ContentType?.MediaType);
#if !NET11_0_OR_GREATER
        Assert.Contains(pageResponse.Headers.GetValues("Set-Cookie"),
            cookie => cookie.StartsWith(".AspNetCore.Antiforgery.", StringComparison.Ordinal));
#else
        Assert.DoesNotContain(pageResponse.Headers.TryGetValues("Set-Cookie", out var cookies) ? cookies : [],
            cookie => cookie.StartsWith(".AspNetCore.Antiforgery.", StringComparison.Ordinal));
#endif

        var page = await pageResponse.Content.ReadAsStringAsync();
        Assert.Contains("<!DOCTYPE html>", page);
        Assert.Contains("Todos using HTMX", page);
        Assert.Contains("htmx.org@", page);
        Assert.Contains("""htmx.config.methodsThatUseUrlParams = ["get"];""", page);
        Assert.Contains("<form id=\"todo-create-form\" hx-post=\"/htmx-todo\"", page);
        Assert.Contains("hx-target=\"#todo-table\"", page);
        Assert.Contains("hx-swap=\"beforeend\"", page);
        Assert.Contains("<tbody id=\"todo-table\">", page);
        Assert.Contains("id=\"todo-error\"", page);
        Assert.Contains("if (event.detail.successful) this.reset()", WebUtility.HtmlDecode(page));
        Assert.Equal(new[] { 1, 2, 3 }, Rows(page).Select(RowId).ToArray());
        var token = RequestToken(page);

        using var created = await MutateAsync(client, HttpMethod.Post, TodoPath, token, ("title", "  A new todo  "));
        var row = await RowFragmentAsync(created);
        var id = RowId(row);
        Assert.True(id > 3);
        Assert.Contains(">A new todo</td>", row);
        Assert.DoesNotContain("  A new todo  ", row);
        Assert.Contains("hx-target=\"this\"", row);
        Assert.Contains("hx-swap=\"outerHTML\"", row);
        Assert.Contains("hx-sync=\"this:drop\"", row);
        Assert.Contains($"hx-put=\"{TodoPath}/{id}\"", row);
        Assert.Contains($"hx-delete=\"{TodoPath}/{id}\"", row);
        AssertState(row, isComplete: false);
        Assert.Equal(row, FindRow(await client.GetStringAsync(TodoPath), id));

        foreach (var isComplete in new[] { true, true, false })
        {
            using var updated = await MutateAsync(client, HttpMethod.Put, $"{TodoPath}/{id}", token,
                ("isComplete", isComplete ? "true" : "false"));
            row = await RowFragmentAsync(updated);
            Assert.Equal(id, RowId(row));
            Assert.Contains(">A new todo</td>", row);
            AssertState(row, isComplete);
            Assert.Equal(row, FindRow(await client.GetStringAsync(TodoPath), id));
        }

        using var deleted = await MutateAsync(client, HttpMethod.Delete, $"{TodoPath}/{id}", token);
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        Assert.Equal(string.Empty, await deleted.Content.ReadAsStringAsync());
        var reloaded = await client.GetStringAsync(TodoPath);
        Assert.DoesNotContain($"id=\"todo-{id}\"", reloaded);
        Assert.Equal(Rows(page), Rows(reloaded));
    }

    [Fact]
    public async Task Create_RejectsInvalidTitlesWithoutChangingDataAndAcceptsMaximumLength()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        var page = await client.GetStringAsync(TodoPath);
        var token = RequestToken(page);

        foreach (var title in new string?[] { null, "", " \t\r\n", new('x', HtmxTodo.MaxTitleLength + 1) })
        {
            var fields = title is null
                ? Array.Empty<(string, string)>()
                : new[] { ("title", title) };
            using var response = await MutateAsync(client, HttpMethod.Post, TodoPath, token, fields);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(Rows(page), Rows(await client.GetStringAsync(TodoPath)));
        }

        var validTitle = new string('x', HtmxTodo.MaxTitleLength);
        using var valid = await MutateAsync(client, HttpMethod.Post, TodoPath, token, ("title", validTitle));
        var row = await RowFragmentAsync(valid);
        Assert.Contains($">{validTitle}</td>", row);
        var reloaded = await client.GetStringAsync(TodoPath);
        Assert.Equal(4, Rows(reloaded).Length);
        Assert.Equal(row, FindRow(reloaded, RowId(row)));
    }

    [Fact]
    public async Task Mutations_ReturnNotFoundForUnknownIdsWithoutChangingData()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        var page = await client.GetStringAsync(TodoPath);
        var token = RequestToken(page);

        using var updated = await MutateAsync(client, HttpMethod.Put, $"{TodoPath}/99999", token,
            ("isComplete", "true"));
        Assert.Equal(HttpStatusCode.NotFound, updated.StatusCode);
        using var deleted = await MutateAsync(client, HttpMethod.Delete, $"{TodoPath}/99999", token);
        Assert.Equal(HttpStatusCode.NotFound, deleted.StatusCode);
        Assert.Equal(Rows(page), Rows(await client.GetStringAsync(TodoPath)));
    }

    [Fact]
    public async Task Create_EncodesUserContentInRowTextAndAttributes()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        var token = RequestToken(await client.GetStringAsync(TodoPath));
        const string title = "<script>alert(\"x\")</script> & 'quoted'";
        var encoded = HtmlEncoder.Default.Encode(title);

        using var created = await MutateAsync(client, HttpMethod.Post, TodoPath, token, ("title", title));
        var row = await RowFragmentAsync(created);
        Assert.Contains($">{encoded}</td>", row);
        Assert.Contains($"aria-label=\"Mark complete: {encoded}\"", row);
        Assert.Contains($"aria-label=\"Delete: {encoded}\"", row);
        Assert.DoesNotContain(title, row);
        Assert.DoesNotContain("<script", row);
        Assert.Equal(row, FindRow(await client.GetStringAsync(TodoPath), RowId(row)));
    }

#if !NET11_0_OR_GREATER
    [Theory]
    [InlineData(null)]
    [InlineData("tampered-token")]
    public async Task Mutations_RejectMissingOrTamperedTokenWithoutChangingData(string? token)
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        var page = await client.GetStringAsync(TodoPath);
        Assert.False(string.IsNullOrWhiteSpace(RequestToken(page)));

        foreach (var method in new[] { HttpMethod.Post, HttpMethod.Put, HttpMethod.Delete })
        {
            using var response = await AttemptMutationAsync(client, method, token);
            Assert.True(response.StatusCode == HttpStatusCode.BadRequest,
                $"{method} with {(token is null ? "a missing" : "a tampered")} antiforgery token: expected 400 BadRequest, got {(int)response.StatusCode} {response.StatusCode}.");
            Assert.Equal(Rows(page), Rows(await client.GetStringAsync(TodoPath)));
        }
    }

    [Fact]
    public async Task Mutations_RejectValidTokenWithoutMatchingCookieWithoutChangingData()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        using var cookielessClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });
        var page = await client.GetStringAsync(TodoPath);
        var token = RequestToken(page);

        foreach (var method in new[] { HttpMethod.Post, HttpMethod.Put, HttpMethod.Delete })
        {
            using var response = await AttemptMutationAsync(cookielessClient, method, token);
            Assert.True(response.StatusCode == HttpStatusCode.BadRequest,
                $"{method} with a valid antiforgery token but no cookie: expected 400 BadRequest, got {(int)response.StatusCode} {response.StatusCode}.");
            Assert.Equal(Rows(page), Rows(await client.GetStringAsync(TodoPath)));
        }
    }
#else
    [Theory]
    [InlineData("same-origin", null)]
    [InlineData("none", null)]
    [InlineData(null, "https://localhost")]
    public async Task Mutations_AcceptTrustedBrowserHeaders(string? fetchSite, string? origin)
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        await client.GetStringAsync(TodoPath);
        if (fetchSite is not null) { client.DefaultRequestHeaders.Add("Sec-Fetch-Site", fetchSite); }
        if (origin is not null) { client.DefaultRequestHeaders.Add("Origin", origin); }

        using var created = await MutateAsync(client, HttpMethod.Post, TodoPath, null, ("title", "Trusted request"));
        var id = RowId(await RowFragmentAsync(created));
        using var updated = await MutateAsync(client, HttpMethod.Put, $"{TodoPath}/{id}", null, ("isComplete", "true"));
        AssertState(await RowFragmentAsync(updated), isComplete: true);
        using var deleted = await MutateAsync(client, HttpMethod.Delete, $"{TodoPath}/{id}", null);
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        Assert.DoesNotContain($"id=\"todo-{id}\"", await client.GetStringAsync(TodoPath));
    }

    [Theory]
    [InlineData("cross-site", "https://attacker.example")]
    [InlineData("same-site", "https://other.localhost")]
    [InlineData(null, "https://attacker.example")]
    [InlineData(null, "http://localhost")]
    [InlineData(null, "https://localhost:8443")]
    public async Task Mutations_RejectUntrustedBrowserHeadersWithoutChangingData(string? fetchSite, string origin)
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        var page = await client.GetStringAsync(TodoPath);
        if (fetchSite is not null) { client.DefaultRequestHeaders.Add("Sec-Fetch-Site", fetchSite); }
        client.DefaultRequestHeaders.Add("Origin", origin);

        foreach (var method in new[] { HttpMethod.Post, HttpMethod.Put, HttpMethod.Delete })
        {
            using var response = await AttemptMutationAsync(client, method, null);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(Rows(page), Rows(await client.GetStringAsync(TodoPath)));
        }
    }
#endif

    [Fact]
    public async Task ConcurrentCreates_StoreEveryTodoWithUniqueIdsAndNeverReuseDeletedIds()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        var page = await client.GetStringAsync(TodoPath);
        var token = RequestToken(page);

        var created = await Task.WhenAll(Enumerable.Range(0, 16).Select(async index =>
        {
            var title = $"Concurrent todo {index}";
            using var response = await MutateAsync(client, HttpMethod.Post, TodoPath, token, ("title", title));
            var row = await RowFragmentAsync(response);
            Assert.Contains($">{title}</td>", row);
            return (Id: RowId(row), Row: row);
        }));

        Assert.Equal(created.Length, created.Select(todo => todo.Id).Distinct().Count());
        Assert.All(created, todo => Assert.True(todo.Id > 3));
        var reloaded = await client.GetStringAsync(TodoPath);
        Assert.Equal(3 + created.Length, Rows(reloaded).Length);
        foreach (var todo in created)
        {
            Assert.Equal(todo.Row, FindRow(reloaded, todo.Id));
        }
        foreach (var seed in Rows(page))
        {
            Assert.Equal(seed, FindRow(reloaded, RowId(seed)));
        }

        var deletedId = created.Max(todo => todo.Id);
        using var deleted = await MutateAsync(client, HttpMethod.Delete, $"{TodoPath}/{deletedId}", token);
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        using var replacement = await MutateAsync(client, HttpMethod.Post, TodoPath, token,
            ("title", "After deletion"));
        var replacementRow = await RowFragmentAsync(replacement);
        Assert.True(RowId(replacementRow) > deletedId);
        reloaded = await client.GetStringAsync(TodoPath);
        Assert.DoesNotContain($"id=\"todo-{deletedId}\"", reloaded);
        Assert.Equal(replacementRow, FindRow(reloaded, RowId(replacementRow)));
        Assert.Equal(3 + created.Length, Rows(reloaded).Length);
    }

    [Fact]
    public async Task HtmxMutations_DoNotChangeExistingStaticDemo()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        var originalIndex = await client.GetStringAsync("/");
        var originalDetail = await client.GetStringAsync("/1");
        Assert.Contains("Todos", originalIndex);
        Assert.Contains("Wash the dishes", originalDetail);
        var token = RequestToken(await client.GetStringAsync(TodoPath));

        using var updated = await MutateAsync(client, HttpMethod.Put, $"{TodoPath}/1", token,
            ("isComplete", "false"));
        AssertState(await RowFragmentAsync(updated), isComplete: false);
        using var deleted = await MutateAsync(client, HttpMethod.Delete, $"{TodoPath}/1", token);
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        using var created = await MutateAsync(client, HttpMethod.Post, TodoPath, token,
            ("title", "Only in the HTMX demo"));
        await RowFragmentAsync(created);

        Assert.Equal(originalIndex, await client.GetStringAsync("/"));
        Assert.Equal(originalDetail, await client.GetStringAsync("/1"));
    }

    private static Task<HttpResponseMessage> AttemptMutationAsync(HttpClient client, HttpMethod method, string? token)
    {
        if (method == HttpMethod.Post)
        {
            return MutateAsync(client, method, TodoPath, token, ("title", "Must not be stored"));
        }

        return MutateAsync(client, method, $"{TodoPath}/2", token, ("isComplete", "true"));
    }

    private static async Task<HttpResponseMessage> MutateAsync(HttpClient client, HttpMethod method,
        string path, string? token, params (string Name, string Value)[] fields)
    {
        using var request = new HttpRequestMessage(method, path);
        var form = fields.Select(field => new KeyValuePair<string, string>(field.Name, field.Value)).ToList();
        if (token is not null)
        {
            form.Add(new(TokenField, token));
        }
        request.Content = new FormUrlEncodedContent(form);

        return await client.SendAsync(request);
    }

    private static string? RequestToken(string html)
    {
#if NET11_0_OR_GREATER
        Assert.DoesNotContain(TokenField, html);
        Assert.DoesNotContain("hx-include=", html);
        return null;
#else
        Assert.Contains("hx-include=\"#todo-create-form input[type=hidden]\"", html);
        var token = Regex.Match(html, """<input type="hidden" name="__RequestVerificationToken" value="(?<token>[^"]+)" />""");
        Assert.True(token.Success, "Expected a hidden antiforgery token rendered by the form helper.");
        return token.Groups["token"].Value;
#endif
    }

    private static string[] Rows(string html) =>
        Regex.Matches(html, """<tr\b[^>]*\bid="todo-\d+"[^>]*>.*?</tr>""", RegexOptions.Singleline)
            .Cast<Match>().Select(match => match.Value).ToArray();

    private static int RowId(string row)
    {
        var match = Regex.Match(row, "\\bid=\"todo-(\\d+)\"");
        Assert.True(match.Success, "Expected a todo row ID.");
        return int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
    }

    private static string FindRow(string html, int id) =>
        Assert.Single(Rows(html), row => RowId(row) == id);

    private static async Task<string> RowFragmentAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(MediaTypeNames.Text.Html, response.Content.Headers.ContentType?.MediaType);
        var html = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("<!DOCTYPE", html);
        Assert.DoesNotContain("<html", html);
        Assert.DoesNotContain("<script", html);
        var row = Assert.Single(Rows(html));
        Assert.Equal(row, html.Trim());
        return row;
    }

    private static void AssertState(string row, bool isComplete)
    {
        Assert.Contains($"<td>{(isComplete ? "Complete" : "Incomplete")}</td>", row);
        var nextValue = isComplete ? "false" : "true";
        Assert.Contains($"hx-vals='{{\"isComplete\": \"{nextValue}\"}}'", WebUtility.HtmlDecode(row));
    }
}
