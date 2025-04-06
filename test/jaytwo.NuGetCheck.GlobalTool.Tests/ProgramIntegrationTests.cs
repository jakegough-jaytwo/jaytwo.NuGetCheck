using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace jaytwo.NuGetCheck.GlobalTool.Tests;

public class ProgramIntegrationTests
{
    [Fact]
    public void Program_Main_returns_success_exit_code_for_happy_path()
    {
        var defaultConsoleOut = Console.Out;
        try
        {
            using (var stringWriter = new StringWriter())
            {
                // arrange
                Console.SetOut(stringWriter);

                var args = new[] { "xunit", "-gte=2.0.0", "-lt=2.0.1" };

                // act
                var result = Program.Main(args);

                // assert
                Assert.Equal(0, result);
                Assert.Equal("2.0.0", stringWriter.ToString().Trim());
            }
        }
        finally
        {
            Console.SetOut(defaultConsoleOut);
        }
    }

    [Fact]
    public void Program_Main_returns_failure_exit_code_for_happy_path_path_fail_on_match()
    {
        var defaultConsoleOut = Console.Out;
        try
        {
            using (var stringWriter = new StringWriter())
            {
                // arrange
                Console.SetOut(stringWriter);

                var args = new[] { "xunit", "-gte=2.0.0", "-lt=2.0.1", "--fail-on-match" };

                // act
                var result = Program.Main(args);

                // assert
                Assert.Equal(-1, result);
                Assert.Equal("2.0.0", stringWriter.ToString().Trim());
            }
        }
        finally
        {
            Console.SetOut(defaultConsoleOut);
        }
    }

    [Fact]
    public void Program_Main_returns_failure_exit_code_for_unhappy_path()
    {
        var defaultConsoleOut = Console.Out;
        try
        {
            using (var stringWriter = new StringWriter())
            {
                // arrange
                Console.SetOut(stringWriter);

                var args = new[] { "xunit", "-gt", "999.999.999" };

                // act
                var result = Program.Main(args);

                // assert
                Assert.Equal(-1, result);
                Assert.Equal("No results.", stringWriter.ToString().Trim());
            }
        }
        finally
        {
            Console.SetOut(defaultConsoleOut);
        }
    }

    [Fact]
    public void Program_Main_returns_success_exit_code_for_unhappy_path_fail_on_match()
    {
        var defaultConsoleOut = Console.Out;
        try
        {
            using (var stringWriter = new StringWriter())
            {
                // arrange
                Console.SetOut(stringWriter);

                var args = new[] { "xunit", "-gt", "999.999.999", "--fail-on-match" };

                // act
                var result = Program.Main(args);

                // assert
                Assert.Equal(0, result);
                Assert.Equal("No results.", stringWriter.ToString().Trim());
            }
        }
        finally
        {
            Console.SetOut(defaultConsoleOut);
        }
    }
}
