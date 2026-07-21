using System.Text;
using NexaStation.Domain;
using NexaStation.Providers.Emulation.Identification;

namespace NexaStation.Providers.Emulation.Tests.Identification;

public sealed class FilenameIdentificationTests
{
    private readonly FilenameGameIdentifier _reader = new();

    [Theory]
    [InlineData("God of War II (USA) (En,Fr,De).iso", "God of War II", "USA", null)]
    [InlineData("Metal Gear Solid (Disc 1).bin", "Metal Gear Solid", null, 1)]
    [InlineData("Racing (Rev 2).iso", "Racing", null, null)]
    [InlineData("Adventure [!].sfc", "Adventure", null, null)]
    [InlineData("Pokémon – Édition (Europe).gba", "Pokémon – Édition", "Europe", null)]
    [InlineData("Title (Director's Cut).iso", "Title (Director's Cut)", null, null)]
    [InlineData("F1 2024 (World).iso", "F1 2024", "World", null)]
    [InlineData("(USA).iso", "(USA)", "USA", null)]
    public async Task NormalizesDeterministically(string file, string title, string? region, int? disc)
    {
        GameIdentificationResult result = await _reader.IdentifyAsync(new("custom", Path.Combine("C:\\Games", file), Path.GetExtension(file)));
        Assert.Equal(title, result.SuggestedTitle); Assert.Equal(file, result.OriginalFileName); Assert.Equal(region, result.Region); Assert.Equal(disc, result.DiscNumber); Assert.Equal(GameIdentificationConfidence.FilenameOnly, result.Confidence);
    }

    [Fact] public async Task FilenameWithoutSerialRemainsLowConfidence() { GameIdentificationResult result = await _reader.IdentifyAsync(new("playstation-2", "C:\\Games\\Unknown Adventure.iso", ".iso")); Assert.Null(result.GameIdentifier); Assert.Equal(GameIdentificationConfidence.FilenameOnly, result.Confidence); }
    [Theory] [InlineData("SCUS_974.81.God of War II.iso", "SCUS-97481", "God of War II")] [InlineData("SLES-53346 Dragon Ball Z Budokai 3.iso", "SLES-53346", "Dragon Ball Z Budokai 3")]
    public async Task ExtractsPs2SerialFromFilenameWithoutReadingDisc(string file, string serial, string title) { GameIdentificationResult result = await _reader.IdentifyAsync(new("playstation-2", file, ".iso")); Assert.Equal(serial, result.GameIdentifier); Assert.Equal(title, result.SuggestedTitle); Assert.Equal(GameIdentificationConfidence.DiscSerial, result.Confidence); }
    [Fact]
    public async Task ReadsOnlyBoundedPs2HeaderForSerial()
    {
        string path = Path.Combine(Path.GetTempPath(), $"NexaStation-ps2-{Guid.NewGuid():N}.iso");
        try { await File.WriteAllBytesAsync(path, [.. new byte[128], .. Encoding.ASCII.GetBytes("BOOT2 = cdrom0:\\SCUS_973.99;1"), .. new byte[1024]]); GameIdentificationResult result = await _reader.IdentifyAsync(new("playstation-2", path, ".iso", 512)); Assert.Equal("SCUS-97399", result.GameIdentifier); }
        finally { File.Delete(path); }
    }
}
