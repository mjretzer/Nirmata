using Gmsd.Agents.Execution.ControlPlane.Tools.Standard;
using Xunit;

namespace Gmsd.Agents.Tests.Execution.ControlPlane.Tools;

public sealed class TrxResultParserTests
{
    [Fact]
    public void ParseTrxFile_NonexistentFile_ReturnsEmptyResult()
    {
        // Arrange
        var parser = new TrxResultParser();
        var filePath = "/nonexistent/path/results.trx";

        // Act
        var result = parser.ParseTrxFile(filePath);

        // Assert
        Assert.Equal(0, result.TotalTests);
        Assert.Equal(0, result.Passed);
        Assert.Equal(0, result.Failed);
        Assert.Empty(result.Failures);
    }

    [Fact]
    public void ParseTrxFile_ValidTrxFile_ParsesCorrectly()
    {
        // Arrange
        var parser = new TrxResultParser();
        var tempFile = Path.GetTempFileName();
        
        try
        {
            // Create a minimal valid TRX file
            var trxContent = """
                <?xml version="1.0" encoding="utf-8"?>
                <TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
                  <ResultSummary>
                    <Counters total="3" executed="3" passed="2" failed="1" />
                  </ResultSummary>
                  <Results>
                    <UnitTestResult testName="Test1" outcome="Passed" />
                    <UnitTestResult testName="Test2" outcome="Passed" />
                    <UnitTestResult testName="Test3" outcome="Failed">
                      <Output>
                        <ErrorInfo>
                          <Message>Test failed</Message>
                          <StackTrace>at TestClass.TestMethod()</StackTrace>
                        </ErrorInfo>
                      </Output>
                    </UnitTestResult>
                  </Results>
                </TestRun>
                """;

            File.WriteAllText(tempFile, trxContent);

            // Act
            var result = parser.ParseTrxFile(tempFile);

            // Assert
            Assert.Equal(3, result.TotalTests);
            Assert.Equal(2, result.Passed);
            Assert.Equal(1, result.Failed);
            Assert.Single(result.Failures);
            Assert.Equal("Test3", result.Failures[0].TestName);
            Assert.Equal("Test failed", result.Failures[0].Message);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void ParseTrxFile_InvalidXml_ReturnsEmptyResult()
    {
        // Arrange
        var parser = new TrxResultParser();
        var tempFile = Path.GetTempFileName();
        
        try
        {
            File.WriteAllText(tempFile, "This is not valid XML");

            // Act
            var result = parser.ParseTrxFile(tempFile);

            // Assert
            Assert.Equal(0, result.TotalTests);
            Assert.Equal(0, result.Passed);
            Assert.Equal(0, result.Failed);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }
}
