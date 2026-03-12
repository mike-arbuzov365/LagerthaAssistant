namespace LagerthaAssistant.IntegrationTests.Services;

using System.IO.Compression;
using System.Security;
using System.Xml.Linq;
using LagerthaAssistant.Application.Interfaces.Vocabulary;
using Microsoft.Extensions.Logging.Abstractions;
using LagerthaAssistant.Application.Models.Vocabulary;
using LagerthaAssistant.Application.Services.Vocabulary;
using LagerthaAssistant.Infrastructure.Options;
using LagerthaAssistant.Infrastructure.Services.Vocabulary;
using Xunit;

public sealed class VocabularyDeckServiceIntegrationTests
{
    [Fact]
    public async Task AppendFromAssistantReplyAsync_ShouldKeepWorkbookReadableAfterSave()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"lagertha-vocabulary-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var workbookPath = Path.Combine(tempDir, "wm-verbs-us-en.xlsx");
            CreateTemplateWorkbook(workbookPath, "test", "(v) ????", "This is a test sentence.");

            var options = new VocabularyDeckOptions
            {
                FolderPath = tempDir,
                FilePattern = "wm-*.xlsx",
                ReadOnlyFileNames = [],
                VerbDeckFileName = "wm-verbs-us-en.xlsx",
                IrregularVerbDeckFileName = "wm-irregular-verbs-ua-en.xlsx",
                FallbackDeckFileName = "wm-verbs-us-en.xlsx"
            };

            var sut = new VocabularyDeckService(options, new VocabularyReplyParser(), NullLogger<VocabularyDeckService>.Instance);

            var response = """
            determine

            (v) ?????????, ?????????????

            We need to determine the best approach.
            """;

            var appendResult = await sut.AppendFromAssistantReplyAsync("determine", response);

            Assert.Equal(VocabularyAppendStatus.Added, appendResult.Status);

            using var archive = ZipFile.OpenRead(workbookPath);
            foreach (var entry in archive.Entries)
            {
                using var stream = entry.Open();
                _ = stream.ReadByte();
            }

            var sheetEntry = archive.GetEntry("xl/worksheets/sheet1.xml");
            Assert.NotNull(sheetEntry);

            using var reader = new StreamReader(sheetEntry!.Open());
            var sheetXml = reader.ReadToEnd();

            Assert.Contains("B12", sheetXml, StringComparison.Ordinal);
            Assert.Contains("determine", sheetXml, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public async Task FindInWritableDecksAsync_ShouldMatchAnyIrregularVerbForm()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"lagertha-vocabulary-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var irregularPath = Path.Combine(tempDir, "wm-irregular-verbs-ua-en.xlsx");
            CreateTemplateWorkbook(irregularPath, "be - was, were - been", "(iv) ????", "We were online all day.");

            var options = new VocabularyDeckOptions
            {
                FolderPath = tempDir,
                FilePattern = "wm-*.xlsx",
                ReadOnlyFileNames = [],
                IrregularVerbDeckFileName = "wm-irregular-verbs-ua-en.xlsx",
                FallbackDeckFileName = "wm-irregular-verbs-ua-en.xlsx"
            };

            var sut = new VocabularyDeckService(options, new VocabularyReplyParser(), NullLogger<VocabularyDeckService>.Instance);

            var lookup = await sut.FindInWritableDecksAsync("were");

            Assert.True(lookup.Found);
            var match = Assert.Single(lookup.Matches);
            Assert.Equal("be - was, were - been", match.Word);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public async Task AppendFromAssistantReplyAsync_ShouldRouteIrregularVerbToIrregularDeck()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"lagertha-vocabulary-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var verbsPath = Path.Combine(tempDir, "wm-verbs-us-en.xlsx");
            var irregularPath = Path.Combine(tempDir, "wm-irregular-verbs-ua-en.xlsx");

            CreateTemplateWorkbook(verbsPath, "build", "(v) ????????", "We build services daily.");
            CreateTemplateWorkbook(irregularPath, "beat - beat - beaten", "(iv) ????", "I beat the estimate.");

            var options = new VocabularyDeckOptions
            {
                FolderPath = tempDir,
                FilePattern = "wm-*.xlsx",
                ReadOnlyFileNames = [],
                VerbDeckFileName = "wm-verbs-us-en.xlsx",
                IrregularVerbDeckFileName = "wm-irregular-verbs-ua-en.xlsx",
                FallbackDeckFileName = "wm-verbs-us-en.xlsx"
            };

            var sut = new VocabularyDeckService(options, new VocabularyReplyParser(), NullLogger<VocabularyDeckService>.Instance);

            var response = """
            bear - bore - born

            (iv) ?????, ???????????

            The service bore high traffic yesterday.

            The load has been borne by the new cluster.
            """;

            var result = await sut.AppendFromAssistantReplyAsync("bore", response);

            Assert.Equal(VocabularyAppendStatus.Added, result.Status);
            Assert.NotNull(result.Entry);
            Assert.Equal("wm-irregular-verbs-ua-en.xlsx", result.Entry!.DeckFileName);

            using var archive = ZipFile.OpenRead(irregularPath);
            var sheetEntry = archive.GetEntry("xl/worksheets/sheet1.xml");
            Assert.NotNull(sheetEntry);
            using var reader = new StreamReader(sheetEntry!.Open());
            var sheetXml = reader.ReadToEnd();
            Assert.Contains("bear - bore - born", sheetXml, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public async Task AppendFromAssistantReplyAsync_ShouldRouteSingleFormIvToVerbDeck()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"lagertha-vocabulary-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var verbsPath = Path.Combine(tempDir, "wm-verbs-us-en.xlsx");
            var irregularPath = Path.Combine(tempDir, "wm-irregular-verbs-ua-en.xlsx");

            CreateTemplateWorkbook(verbsPath, "build", "(v) create software", "We build services daily.");
            CreateTemplateWorkbook(irregularPath, "beat - beat - beaten", "(iv) hit", "I beat the estimate.");

            var options = new VocabularyDeckOptions
            {
                FolderPath = tempDir,
                FilePattern = "wm-*.xlsx",
                ReadOnlyFileNames = [],
                VerbDeckFileName = "wm-verbs-us-en.xlsx",
                IrregularVerbDeckFileName = "wm-irregular-verbs-ua-en.xlsx",
                FallbackDeckFileName = "wm-verbs-us-en.xlsx"
            };

            var sut = new VocabularyDeckService(options, new VocabularyReplyParser(), NullLogger<VocabularyDeckService>.Instance);

            var response = """
            prepare

            (iv) get ready, make something ready

            We prepare the deployment scripts before the release.
            """;

            var result = await sut.AppendFromAssistantReplyAsync("prepare", response);

            Assert.Equal(VocabularyAppendStatus.Added, result.Status);
            Assert.NotNull(result.Entry);
            Assert.Equal("wm-verbs-us-en.xlsx", result.Entry!.DeckFileName);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public async Task AppendFromAssistantReplyAsync_ShouldSavePersistentExpressionToPersistentDeck_WithoutExamples()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"lagertha-vocabulary-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var verbsPath = Path.Combine(tempDir, "wm-verbs-us-en.xlsx");
            var persistentPath = Path.Combine(tempDir, "wm-persistant-expressions-ua-en.xlsx");

            CreateTemplateWorkbook(verbsPath, "build", "(v) create software", "We build services daily.");
            CreateTemplateWorkbook(persistentPath, "On purpose", "(pe) навмисно", string.Empty);

            var options = new VocabularyDeckOptions
            {
                FolderPath = tempDir,
                FilePattern = "wm-*.xlsx",
                ReadOnlyFileNames = [],
                VerbDeckFileName = "wm-verbs-us-en.xlsx",
                PersistentExpressionDeckFileName = "wm-persistant-expressions-ua-en.xlsx",
                FallbackDeckFileName = "wm-verbs-us-en.xlsx"
            };

            var sut = new VocabularyDeckService(options, new VocabularyReplyParser(), NullLogger<VocabularyDeckService>.Instance);

            var response = """
on the same page

(pe) мати спільне розуміння
""";

            var result = await sut.AppendFromAssistantReplyAsync("on the same page", response);

            Assert.Equal(VocabularyAppendStatus.Added, result.Status);
            Assert.NotNull(result.Entry);
            Assert.Equal("wm-persistant-expressions-ua-en.xlsx", result.Entry!.DeckFileName);
            Assert.Equal("On the same page", result.Entry.Word);
            Assert.True(string.IsNullOrWhiteSpace(result.Entry.Examples));
            Assert.Contains("(pe)", result.Entry.Meaning, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
    [Fact]
    public async Task AppendFromAssistantReplyAsync_ShouldRoutePhrasalVerbToPhrasalDeck()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"lagertha-vocabulary-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var verbsPath = Path.Combine(tempDir, "wm-verbs-us-en.xlsx");
            var phrasalPath = Path.Combine(tempDir, "wm-phrasal-verbs-ua-en.xlsx");

            CreateTemplateWorkbook(verbsPath, "build", "(v) ????????", "We build services daily.");
            CreateTemplateWorkbook(phrasalPath, "look up", "(pv) ??????", "I will look up the docs.");

            var options = new VocabularyDeckOptions
            {
                FolderPath = tempDir,
                FilePattern = "wm-*.xlsx",
                ReadOnlyFileNames = [],
                VerbDeckFileName = "wm-verbs-us-en.xlsx",
                PhrasalVerbDeckFileName = "wm-phrasal-verbs-ua-en.xlsx",
                FallbackDeckFileName = "wm-verbs-us-en.xlsx"
            };

            var sut = new VocabularyDeckService(options, new VocabularyReplyParser(), NullLogger<VocabularyDeckService>.Instance);

            var response = """
            call back

            (pv) call in return, phone someone in response

            Please call back the client after the meeting.
            """;

            var result = await sut.AppendFromAssistantReplyAsync("call back", response);

            Assert.Equal(VocabularyAppendStatus.Added, result.Status);
            Assert.NotNull(result.Entry);
            Assert.Equal("wm-phrasal-verbs-ua-en.xlsx", result.Entry!.DeckFileName);

            using var archive = ZipFile.OpenRead(phrasalPath);
            var sheetEntry = archive.GetEntry("xl/worksheets/sheet1.xml");
            Assert.NotNull(sheetEntry);
            using var reader = new StreamReader(sheetEntry!.Open());
            var sheetXml = reader.ReadToEnd();
            Assert.Contains("call back", sheetXml, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public async Task AppendFromAssistantReplyAsync_ShouldUseForcedDeckAndOverridePartOfSpeech()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"lagertha-vocabulary-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var verbsPath = Path.Combine(tempDir, "wm-verbs-us-en.xlsx");
            var prepositionsPath = Path.Combine(tempDir, "wm-prepositions-ua-en.xlsx");

            CreateTemplateWorkbook(verbsPath, "call", "(v) ??????????", "Call me later.");
            CreateTemplateWorkbook(prepositionsPath, "under", "(prep) ???", "Under heavy load, latency grows.");

            var options = new VocabularyDeckOptions
            {
                FolderPath = tempDir,
                FilePattern = "wm-*.xlsx",
                ReadOnlyFileNames = [],
                VerbDeckFileName = "wm-verbs-us-en.xlsx",
                PrepositionDeckFileName = "wm-prepositions-ua-en.xlsx",
                FallbackDeckFileName = "wm-verbs-us-en.xlsx"
            };

            var sut = new VocabularyDeckService(options, new VocabularyReplyParser(), NullLogger<VocabularyDeckService>.Instance);

            var response = """
            call back

            (v) call in return, phone someone in response

            Please call back the client after the meeting.
            """;

            var result = await sut.AppendFromAssistantReplyAsync(
                "call back",
                response,
                forcedDeckFileName: "wm-prepositions-ua-en.xlsx",
                overridePartOfSpeech: "prep");

            Assert.Equal(VocabularyAppendStatus.Added, result.Status);
            Assert.NotNull(result.Entry);
            Assert.Equal("wm-prepositions-ua-en.xlsx", result.Entry!.DeckFileName);

            var lookup = await sut.FindInWritableDecksAsync("call back");
            var saved = Assert.Single(lookup.Matches, x => x.DeckFileName.Equals("wm-prepositions-ua-en.xlsx", StringComparison.OrdinalIgnoreCase));
            Assert.Contains("(prep)", saved.Meaning, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
    [Fact]
    public async Task FindInWritableDecksAsync_ShouldReadWhenWorkbookIsOpenedByAnotherProcess()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"lagertha-vocabulary-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var workbookPath = Path.Combine(tempDir, "wm-verbs-us-en.xlsx");
            CreateTemplateWorkbook(workbookPath, "bore", "(v) ???????? ??????", "This lesson will bore you.");

            var options = new VocabularyDeckOptions
            {
                FolderPath = tempDir,
                FilePattern = "wm-*.xlsx",
                ReadOnlyFileNames = [],
                VerbDeckFileName = "wm-verbs-us-en.xlsx",
                FallbackDeckFileName = "wm-verbs-us-en.xlsx"
            };

            using var lockStream = new FileStream(workbookPath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
            var sut = new VocabularyDeckService(options, new VocabularyReplyParser(), NullLogger<VocabularyDeckService>.Instance);

            var lookup = await sut.FindInWritableDecksAsync("bore");

            Assert.True(lookup.Found);
            var match = Assert.Single(lookup.Matches);
            Assert.Equal("wm-verbs-us-en.xlsx", match.DeckFileName);
            Assert.Equal("bore", match.Word);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public async Task AppendFromAssistantReplyAsync_ShouldReturnFriendlyMessageWhenWorkbookIsLocked()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"lagertha-vocabulary-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var workbookPath = Path.Combine(tempDir, "wm-verbs-us-en.xlsx");
            CreateTemplateWorkbook(workbookPath, "build", "(v) ????????", "We build services daily.");

            var options = new VocabularyDeckOptions
            {
                FolderPath = tempDir,
                FilePattern = "wm-*.xlsx",
                ReadOnlyFileNames = [],
                VerbDeckFileName = "wm-verbs-us-en.xlsx",
                FallbackDeckFileName = "wm-verbs-us-en.xlsx"
            };

            using var lockStream = new FileStream(workbookPath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
            var sut = new VocabularyDeckService(options, new VocabularyReplyParser(), NullLogger<VocabularyDeckService>.Instance);

            var response = """
            deploy

            (v) ??????????

            We deploy every Friday.
            """;

            var result = await sut.AppendFromAssistantReplyAsync("deploy", response);

            Assert.Equal(VocabularyAppendStatus.Error, result.Status);
            Assert.Contains("file is open in another app", result.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public async Task FindInWritableDecksAsync_ShouldMatchMinorTypoForSingleWord()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"lagertha-vocabulary-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var workbookPath = Path.Combine(tempDir, "wm-verbs-us-en.xlsx");
            CreateTemplateWorkbook(workbookPath, "undertake", "(v) take responsibility for a task", "We undertake migrations every quarter.");

            var options = new VocabularyDeckOptions
            {
                FolderPath = tempDir,
                FilePattern = "wm-*.xlsx",
                ReadOnlyFileNames = [],
                VerbDeckFileName = "wm-verbs-us-en.xlsx",
                FallbackDeckFileName = "wm-verbs-us-en.xlsx"
            };

            var sut = new VocabularyDeckService(options, new VocabularyReplyParser(), NullLogger<VocabularyDeckService>.Instance);

            var lookup = await sut.FindInWritableDecksAsync("undertak");

            Assert.True(lookup.Found);
            var match = Assert.Single(lookup.Matches);
            Assert.Equal("undertake", match.Word);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public async Task PreviewAppendFromAssistantReplyAsync_ShouldDetectDuplicateForMinorTypoWord()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"lagertha-vocabulary-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var workbookPath = Path.Combine(tempDir, "wm-verbs-us-en.xlsx");
            CreateTemplateWorkbook(workbookPath, "undertake", "(v) take responsibility for a task", "We undertake migrations every quarter.");

            var options = new VocabularyDeckOptions
            {
                FolderPath = tempDir,
                FilePattern = "wm-*.xlsx",
                ReadOnlyFileNames = [],
                VerbDeckFileName = "wm-verbs-us-en.xlsx",
                FallbackDeckFileName = "wm-verbs-us-en.xlsx"
            };

            var sut = new VocabularyDeckService(options, new VocabularyReplyParser(), NullLogger<VocabularyDeckService>.Instance);

            var response = """
            undertak

            (v) take responsibility for a task

            We undertak infrastructure tasks.
            """;

            var preview = await sut.PreviewAppendFromAssistantReplyAsync("undertak", response);

            Assert.Equal(VocabularyAppendPreviewStatus.DuplicateFound, preview.Status);
            Assert.NotNull(preview.DuplicateMatches);
            Assert.NotEmpty(preview.DuplicateMatches!);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }


    [Fact]
    public async Task PreviewThenAppend_ShouldReusePreparedPlan_AndSkipSecondParse()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"lagertha-vocabulary-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var workbookPath = Path.Combine(tempDir, "wm-verbs-us-en.xlsx");
            CreateTemplateWorkbook(workbookPath, "build", "(v) to build software components", "We build services daily.");

            var options = new VocabularyDeckOptions
            {
                FolderPath = tempDir,
                FilePattern = "wm-*.xlsx",
                ReadOnlyFileNames = [],
                VerbDeckFileName = "wm-verbs-us-en.xlsx",
                FallbackDeckFileName = "wm-verbs-us-en.xlsx"
            };

            var parsedReply = new ParsedVocabularyReply(
                "void",
                ["(v) no return value"],
                ["The function returns void when there is no value to return."],
                ["v"]);

            var parser = new CountingReplyParser(parsedReply, throwOnSecondCall: true);
            var sut = new VocabularyDeckService(options, parser, NullLogger<VocabularyDeckService>.Instance);

            const string assistantReply = "raw-reply";

            var preview = await sut.PreviewAppendFromAssistantReplyAsync("void", assistantReply);
            Assert.Equal(VocabularyAppendPreviewStatus.ReadyToAppend, preview.Status);

            var append = await sut.AppendFromAssistantReplyAsync("void", assistantReply);
            Assert.Equal(VocabularyAppendStatus.Added, append.Status);
            Assert.Equal(1, parser.TryParseCalls);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    private sealed class CountingReplyParser : IVocabularyReplyParser
    {
        private readonly ParsedVocabularyReply _parsedReply;
        private readonly bool _throwOnSecondCall;

        public CountingReplyParser(ParsedVocabularyReply parsedReply, bool throwOnSecondCall)
        {
            _parsedReply = parsedReply;
            _throwOnSecondCall = throwOnSecondCall;
        }

        public int TryParseCalls { get; private set; }

        public bool TryParse(string assistantReply, out ParsedVocabularyReply? parsedReply)
        {
            TryParseCalls++;

            if (_throwOnSecondCall && TryParseCalls > 1)
            {
                throw new InvalidOperationException("Parser should not be called more than once for preview->append flow.");
            }

            parsedReply = _parsedReply;
            return true;
        }
    }
    private static void CreateTemplateWorkbook(string workbookPath, string initialWord, string initialMeaning, string initialExamples)
    {
        using var archive = ZipFile.Open(workbookPath, ZipArchiveMode.Create);

        WriteEntry(archive, "[Content_Types].xml", """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml" />
  <Default Extension="xml" ContentType="application/xml" />
  <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml" />
  <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml" />
</Types>
""");

        WriteEntry(archive, "_rels/.rels", """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml" />
</Relationships>
""");

        WriteEntry(archive, "xl/workbook.xml", """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
  <sheets>
    <sheet name="Sheet1" sheetId="1" r:id="rId1" />
  </sheets>
</workbook>
""");

        WriteEntry(archive, "xl/_rels/workbook.xml.rels", """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml" />
</Relationships>
""");

        var escapedMeaning = EscapeXml(initialMeaning);
        var escapedWord = EscapeXml(initialWord);
        var escapedExamples = EscapeXml(initialExamples);

        WriteEntry(archive, "xl/worksheets/sheet1.xml", $"""
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
  <sheetData>
    <row r="10">
      <c r="A10" t="inlineStr"><is><t>Text 1</t></is></c>
      <c r="B10" t="inlineStr"><is><t>Text 2</t></is></c>
      <c r="H10" t="inlineStr"><is><t>Text 3</t></is></c>
    </row>
    <row r="11">
      <c r="A11" t="inlineStr"><is><t>{escapedMeaning}</t></is></c>
      <c r="B11" t="inlineStr"><is><t>{escapedWord}</t></is></c>
      <c r="H11" t="inlineStr"><is><t>{escapedExamples}</t></is></c>
    </row>
  </sheetData>
</worksheet>
""");
    }

    private static string EscapeXml(string value)
    {
        return SecurityElement.Escape(value) ?? string.Empty;
    }

    private static void WriteEntry(ZipArchive archive, string entryPath, string content)
    {
        var entry = archive.CreateEntry(entryPath, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open());
        writer.Write(content);
    }
}



