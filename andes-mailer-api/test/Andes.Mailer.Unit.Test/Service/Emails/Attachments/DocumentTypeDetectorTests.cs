using Andes.Mailer.Common.Enums;
using Andes.Mailer.Service.Emails.Attachments;
using Andes.Mailer.Unit.Test.TestInfrastructure;

namespace Andes.Mailer.Unit.Test.Service.Emails.Attachments;

public sealed class DocumentTypeDetectorTests
{
    public static TheoryData<DocumentTypes, byte[]> Signatures => new()
    {
        { DocumentTypes.Pdf, TestFiles.Pdf },
        { DocumentTypes.Docx, TestFiles.Zip },
        { DocumentTypes.Pptx, TestFiles.Zip },
        { DocumentTypes.Png, TestFiles.Png },
        { DocumentTypes.Jpeg, TestFiles.Jpeg },
        { DocumentTypes.Gif, TestFiles.Gif },
        { DocumentTypes.Webp, TestFiles.Webp },
    };

    public static TheoryData<DocumentTypes> AllTypes => [.. Enum.GetValues<DocumentTypes>()];

    [Theory]
    [InlineData("report.pdf", DocumentTypes.Pdf)]
    [InlineData("REPORT.PDF", DocumentTypes.Pdf)]
    [InlineData("letter.docx", DocumentTypes.Docx)]
    [InlineData("deck.pptx", DocumentTypes.Pptx)]
    [InlineData("photo.jpg", DocumentTypes.Jpeg)]
    [InlineData("photo.jpeg", DocumentTypes.Jpeg)]
    [InlineData("chart.png", DocumentTypes.Png)]
    [InlineData("anim.gif", DocumentTypes.Gif)]
    [InlineData("image.webp", DocumentTypes.Webp)]
    public void FromFileName_AcceptedExtension_ReturnsType(string fileName, DocumentTypes expected)
    {
        Assert.Equal(expected, DocumentTypeDetector.FromFileName(fileName));
    }

    [Theory]
    [InlineData("setup.exe")]
    [InlineData("legacy.doc")]
    [InlineData("notes.txt")]
    [InlineData("report.pdf.exe")]
    [InlineData("no-extension")]
    public void FromFileName_OtherExtension_ReturnsNull(string fileName)
    {
        Assert.Null(DocumentTypeDetector.FromFileName(fileName));
    }

    [Theory]
    [MemberData(nameof(Signatures))]
    public void Detect_MatchingSignature_ReturnsTrue(DocumentTypes type, byte[] header)
    {
        Assert.True(DocumentTypeDetector.Detect(type, header));
    }

    [Theory]
    [InlineData(DocumentTypes.Pdf)]
    [InlineData(DocumentTypes.Docx)]
    [InlineData(DocumentTypes.Png)]
    [InlineData(DocumentTypes.Jpeg)]
    public void Detect_RenamedExecutable_ReturnsFalse(DocumentTypes type)
    {
        Assert.False(DocumentTypeDetector.Detect(type, TestFiles.Executable));
    }

    [Fact]
    public void Detect_RiffWithoutWebpMarker_ReturnsFalse()
    {
        Assert.False(DocumentTypeDetector.Detect(DocumentTypes.Webp, "RIFF$\u0000\u0000\u0000WAVE"u8));
    }

    [Theory]
    [MemberData(nameof(AllTypes))]
    public void ContentTypeOf_EveryType_HasMediaType(DocumentTypes type)
    {
        Assert.Contains("/", DocumentTypeDetector.ContentTypeOf(type), StringComparison.Ordinal);
    }
}
