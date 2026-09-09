param(
    [Parameter(Mandatory = $true)]
    [string] $IndexHtml,

    [Parameter(Mandatory = $true)]
    [string] $Template,

    [Parameter(Mandatory = $true)]
    [string] $Output
)

$ErrorActionPreference = 'Stop'

# .NET 10 standalone Blazor publishing generates an inline import map whose
# exact contents are part of the immutable frontend artifact. Hash those bytes
# for CSP instead of permitting arbitrary inline scripts.
$html = [System.IO.File]::ReadAllText($IndexHtml)
$matches = [System.Text.RegularExpressions.Regex]::Matches(
    $html,
    '<script type="importmap">(?<content>.*?)</script>',
    [System.Text.RegularExpressions.RegexOptions]::Singleline)

if ($matches.Count -ne 1) {
    throw "Expected exactly one generated import map in '$IndexHtml', found $($matches.Count)."
}

$importMap = $matches[0].Groups['content'].Value
if ([string]::IsNullOrEmpty($importMap)) {
    throw "The generated import map in '$IndexHtml' is empty."
}

$bytes = [System.Text.Encoding]::UTF8.GetBytes($importMap)
$hash = [Convert]::ToBase64String([System.Security.Cryptography.SHA256]::HashData($bytes))
$source = "sha256-$hash"

$templateText = [System.IO.File]::ReadAllText($Template)
if (-not $templateText.Contains('__UFW_IMPORTMAP_SHA256__', [System.StringComparison]::Ordinal)) {
    throw "CSP template '$Template' does not contain the import-map hash placeholder."
}

$rendered = $templateText.Replace('__UFW_IMPORTMAP_SHA256__', $source, [System.StringComparison]::Ordinal)
[System.IO.File]::WriteAllText($Output, $rendered, [System.Text.UTF8Encoding]::new($false))

if (-not $rendered.Contains("'$source'", [System.StringComparison]::Ordinal)) {
    throw 'Rendered CSP does not contain the generated import-map source hash.'
}
