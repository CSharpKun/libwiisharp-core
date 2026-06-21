using System.Security.Cryptography;

namespace LibWiiSharpCore.Tests;

public sealed class NusClientTests
{
    [Test]
    [ClassDataSource<NusClientFixture>]
    public async Task DownloadTitle_CorrectTitle_EqualsToOriginalLib(NusClientFixture fixture)
    {
        await fixture.TestingNusClient.DownloadTitle(
            fixture.TestingTempDirectory.FullName,
            "0000000100000002",
            "513"
        );

        fixture.OriginalNusClient.DownloadTitle(
            "0000000100000002",
            "513",
            fixture.OriginalTempDirectory.FullName,
            libWiiSharp.StoreType.All
        );

        var testingFiles = Directory.GetFiles(fixture.TestingTempDirectory.FullName);

        foreach (var testingFilePath in testingFiles)
        {
            var originalFilePath = Path.Combine(
                fixture.OriginalTempDirectory.FullName,
                Path.GetFileName(testingFilePath)
            );
            var testingHash = SHA256.HashData(await File.ReadAllBytesAsync(testingFilePath));
            var originalHash = SHA256.HashData(await File.ReadAllBytesAsync(originalFilePath));
            await Assert.That(testingHash).IsEquivalentTo(originalHash);
        }
    }

    private static byte[] GetFileFromFolder(string folder, string file) =>
        File.ReadAllBytes(Path.Combine(folder, file));
}
