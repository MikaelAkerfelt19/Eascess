using HtmlAgilityPack;

namespace Eascess_Infrastructure.Scanning;

/// <summary>
/// HtmlAgilityPack ile parse edilmiş bir HTML dökümanını WCAG 2.2
/// kurallarına göre denetler ve bulunan sorunları döner.
/// </summary>
internal static class WcagAnalyzer
{
    public static IReadOnlyList<WcagIssue> Analyze(HtmlDocument doc)
    {
        var issues = new List<WcagIssue>();

        issues.AddRange(CheckImages(doc));
        issues.AddRange(CheckLinks(doc));
        issues.AddRange(CheckDocumentLanguage(doc));
        issues.AddRange(CheckPageTitle(doc));
        issues.AddRange(CheckFormLabels(doc));
        issues.AddRange(CheckButtons(doc));
        issues.AddRange(CheckIframes(doc));
        issues.AddRange(CheckHeadings(doc));
        issues.AddRange(CheckInputTypes(doc));

        return issues;
    }

    // ── 1. Görseller (WCAG 1.1.1) ─────────────────────────────────────────
    private static IEnumerable<WcagIssue> CheckImages(HtmlDocument doc)
    {
        foreach (var img in doc.DocumentNode.SelectNodes("//img") ?? Enumerable.Empty<HtmlNode>())
        {
            var alt = img.GetAttributeValue("alt", null);
            var src = img.GetAttributeValue("src", "img");
            var sel = $"img[src=\"{Truncate(src, 60)}\"]";

            var role = img.GetAttributeValue("role", "");
            var ariaHidden = img.GetAttributeValue("aria-hidden", "false");

            if (ariaHidden == "true" || role == "presentation") continue; // dekoratif

            if (alt is null)
            {
                yield return new WcagIssue(
                    "MISSING_ALT_TEXT",
                    "Görsel için alt metni (alt attribute) eksik. Ekran okuyucular görseli tanımlayamaz.",
                    "A", "Critical", sel);
            }
            else if (alt.Trim().Length == 0)
            {
                // Boş alt — dekoratif değil mi kontrol et (context'e göre)
                if (!IsLikelyDecorative(img))
                {
                    yield return new WcagIssue(
                        "EMPTY_ALT_TEXT",
                        "Görsel içeriği açıklayan bir alt metni bulunmuyor. Boş alt metni yalnızca dekoratif görsellerde kullanın.",
                        "A", "Warning", sel);
                }
            }
        }
    }

    // ── 2. Bağlantılar (WCAG 2.4.4) ───────────────────────────────────────
    private static IEnumerable<WcagIssue> CheckLinks(HtmlDocument doc)
    {
        var genericTexts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "click here", "here", "more", "read more", "link", "tıklayın", "buraya", "devamı", "daha fazla" };

        foreach (var a in doc.DocumentNode.SelectNodes("//a[@href]") ?? Enumerable.Empty<HtmlNode>())
        {
            var text  = a.InnerText.Trim();
            var label = a.GetAttributeValue("aria-label", "").Trim();
            var href  = a.GetAttributeValue("href", "#");
            var sel   = $"a[href=\"{Truncate(href, 60)}\"]";

            if (text.Length == 0 && label.Length == 0)
            {
                var hasImg = a.SelectSingleNode(".//img[@alt]") is not null;
                if (!hasImg)
                    yield return new WcagIssue(
                        "EMPTY_LINK_TEXT",
                        "Bağlantının görünür metni veya aria-label özelliği yok. Ekran okuyucular amacını aktaramaz.",
                        "A", "Critical", sel);
            }
            else if (label.Length == 0 && genericTexts.Contains(text))
            {
                yield return new WcagIssue(
                    "GENERIC_LINK_TEXT",
                    $"'{text}' bağlantı metni yeterince açıklayıcı değil. Bağlantının amacını belirten bir metin kullanın.",
                    "AA", "Warning", sel);
            }
        }
    }

    // ── 3. Belge dili (WCAG 3.1.1) ────────────────────────────────────────
    private static IEnumerable<WcagIssue> CheckDocumentLanguage(HtmlDocument doc)
    {
        var html = doc.DocumentNode.SelectSingleNode("//html");
        if (html is null) yield break;

        var lang = html.GetAttributeValue("lang", "").Trim();
        if (lang.Length == 0)
            yield return new WcagIssue(
                "MISSING_LANG_ATTR",
                "<html> etiketinde lang özelliği eksik. Ekran okuyucular doğru dili belirleyemez.",
                "A", "Critical", "html");
    }

    // ── 4. Sayfa başlığı (WCAG 2.4.2) ─────────────────────────────────────
    private static IEnumerable<WcagIssue> CheckPageTitle(HtmlDocument doc)
    {
        var title = doc.DocumentNode.SelectSingleNode("//head/title");
        if (title is null || title.InnerText.Trim().Length == 0)
            yield return new WcagIssue(
                "MISSING_PAGE_TITLE",
                "Sayfa başlığı (<title>) eksik veya boş. Her sayfanın açıklayıcı bir başlığı olmalı.",
                "A", "Critical", "head > title");
    }

    // ── 5. Form etiketleri (WCAG 1.3.1) ───────────────────────────────────
    private static IEnumerable<WcagIssue> CheckFormLabels(HtmlDocument doc)
    {
        var labelledIds = new HashSet<string>();
        foreach (var lbl in doc.DocumentNode.SelectNodes("//label[@for]") ?? Enumerable.Empty<HtmlNode>())
            labelledIds.Add(lbl.GetAttributeValue("for", ""));

        var inputTypes = new[] { "text", "email", "password", "tel", "url", "number", "search", "date", "time" };

        foreach (var input in doc.DocumentNode.SelectNodes("//input") ?? Enumerable.Empty<HtmlNode>())
        {
            var type  = input.GetAttributeValue("type", "text").ToLower();
            if (!inputTypes.Contains(type)) continue;

            var id          = input.GetAttributeValue("id", "");
            var ariaLabel   = input.GetAttributeValue("aria-label", "").Trim();
            var ariaLabelBy = input.GetAttributeValue("aria-labelledby", "").Trim();
            var placeholder = input.GetAttributeValue("placeholder", "").Trim();
            var hidden      = input.GetAttributeValue("type", "").Equals("hidden", StringComparison.OrdinalIgnoreCase);

            if (hidden) continue;

            bool hasLabel = (id.Length > 0 && labelledIds.Contains(id))
                         || ariaLabel.Length > 0
                         || ariaLabelBy.Length > 0;

            if (!hasLabel)
            {
                var sel = id.Length > 0 ? $"input#{id}" : $"input[type=\"{type}\"]";
                yield return new WcagIssue(
                    "MISSING_FORM_LABEL",
                    $"Giriş alanı ({type}) için <label> veya aria-label eksik. Ekran okuyucular alanı tanımlayamaz.",
                    "A", "Critical", sel);
            }
        }
    }

    // ── 6. Butonlar (WCAG 4.1.2) ──────────────────────────────────────────
    private static IEnumerable<WcagIssue> CheckButtons(HtmlDocument doc)
    {
        foreach (var btn in doc.DocumentNode.SelectNodes("//button") ?? Enumerable.Empty<HtmlNode>())
        {
            var text       = btn.InnerText.Trim();
            var ariaLabel  = btn.GetAttributeValue("aria-label", "").Trim();
            var ariaLabelBy= btn.GetAttributeValue("aria-labelledby", "").Trim();
            var title      = btn.GetAttributeValue("title", "").Trim();
            var hasImg     = btn.SelectSingleNode(".//img[@alt]") is not null;

            if (text.Length == 0 && ariaLabel.Length == 0 && ariaLabelBy.Length == 0 && title.Length == 0 && !hasImg)
            {
                var id  = btn.GetAttributeValue("id", "");
                var sel = id.Length > 0 ? $"button#{id}" : "button";
                yield return new WcagIssue(
                    "BUTTON_NO_NAME",
                    "Butonun erişilebilir adı (metin, aria-label, title) eksik. Ekran okuyucular amacını aktaramaz.",
                    "A", "Critical", sel);
            }
        }
    }

    // ── 7. iFrame başlıkları (WCAG 2.4.1) ─────────────────────────────────
    private static IEnumerable<WcagIssue> CheckIframes(HtmlDocument doc)
    {
        foreach (var frame in doc.DocumentNode.SelectNodes("//iframe") ?? Enumerable.Empty<HtmlNode>())
        {
            var title = frame.GetAttributeValue("title", "").Trim();
            var src   = frame.GetAttributeValue("src", "iframe");
            if (title.Length == 0)
                yield return new WcagIssue(
                    "IFRAME_NO_TITLE",
                    "iframe için title özelliği eksik. Ekran okuyucular içeriği tanımlayamaz.",
                    "A", "Warning", $"iframe[src=\"{Truncate(src, 60)}\"]");
        }
    }

    // ── 8. Başlık hiyerarşisi (WCAG 1.3.1) ───────────────────────────────
    private static IEnumerable<WcagIssue> CheckHeadings(HtmlDocument doc)
    {
        var h1List = doc.DocumentNode.SelectNodes("//h1") ?? Enumerable.Empty<HtmlNode>();
        var allH   = doc.DocumentNode.SelectNodes("//h1|//h2|//h3|//h4|//h5|//h6");

        if (allH is null || !allH.Any())
        {
            yield return new WcagIssue(
                "NO_HEADINGS",
                "Sayfada hiç başlık etiketi (h1-h6) bulunmuyor. Başlıklar sayfa yapısını tanımlar.",
                "AA", "Warning", "body");
            yield break;
        }

        if (!h1List.Any())
            yield return new WcagIssue(
                "MISSING_H1",
                "Sayfada <h1> başlığı yok. Her sayfada sayfanın ana konusunu tanımlayan tek bir h1 olmalı.",
                "AA", "Warning", "body");

        if (h1List.Count() > 1)
            yield return new WcagIssue(
                "MULTIPLE_H1",
                $"Sayfada {h1List.Count()} tane <h1> var. Genellikle her sayfada yalnızca bir h1 kullanılmalıdır.",
                "AA", "Info", "h1");
    }

    // ── 9. Giriş tipi label eksik autocomplete (WCAG 1.3.5) ───────────────
    private static IEnumerable<WcagIssue> CheckInputTypes(HtmlDocument doc)
    {
        var passwordInputs = doc.DocumentNode
            .SelectNodes("//input[@type='password']") ?? Enumerable.Empty<HtmlNode>();

        foreach (var pwd in passwordInputs)
        {
            var autoComplete = pwd.GetAttributeValue("autocomplete", "").ToLower();
            if (autoComplete != "current-password" && autoComplete != "new-password")
            {
                var id  = pwd.GetAttributeValue("id", "");
                var sel = id.Length > 0 ? $"input#{id}" : "input[type=\"password\"]";
                yield return new WcagIssue(
                    "PASSWORD_NO_AUTOCOMPLETE",
                    "Şifre alanında autocomplete='current-password' veya 'new-password' eksik. Şifre yöneticileri alanı tanımlayamaz.",
                    "AA", "Info", sel);
            }
        }
    }

    // ── Yardımcılar ───────────────────────────────────────────────────────
    private static bool IsLikelyDecorative(HtmlNode img)
    {
        var src = img.GetAttributeValue("src", "").ToLower();
        return src.Contains("spacer") || src.Contains("blank") || src.Contains("pixel")
            || src.EndsWith(".gif") && img.GetAttributeValue("width", "1") == "1";
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";
}
