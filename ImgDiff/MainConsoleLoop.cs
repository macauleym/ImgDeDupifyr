using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using ImgDiff.Builders;
using ImgDiff.Constants;
using ImgDiff.Factories;
using ImgDiff.Models;
using ImgDiff.Monads;

namespace ImgDiff
{
    public class MainConsoleLoop
    {
        readonly ComparisonRequestFactory requestFactory  = new ComparisonRequestFactory();
        readonly ImageComparisonFactory comparisonFactory = new ImageComparisonFactory();

        static readonly string validExtensionsCombined = ValidExtensions.ForImage.Aggregate((total, next) => $"{total}, {next}");
        
        public async Task Execute(ComparisonOptions initialOptions)
        {
            do
            {
                Console.Write("DeDupifyr> ");
                
                var inputString = Console.ReadLine();
                if (string.IsNullOrEmpty(inputString))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(
                        "You must enter either a directory ('C:\\to\\some\\directory'), or a pair of files separated by a coma ('C:\\path\\to\\first.png,C:\\path\\to\\second.jpg').");
                    Console.ForegroundColor = ConsoleColor.White;

                    continue;
                }

                if (CommandIsGiven(inputString, ProgramCommands.ForTermination))
                    break;

                if (CommandIsGiven(inputString, ProgramCommands.ForHelp))
                {
                    OutputHelpText();
                    
                    continue;
                }
                
                if (CommandIsGiven(inputString, ProgramCommands.ToChangeOptions))
                {
                    initialOptions = OverwriteComparisonOptions(initialOptions);
                    
                    // We need to continue here, so that we can reset the console 
                    // input. Otherwise we'll accidentally attempt to process "options"
                    // as a directory.
                    continue;
                }

                var comparisonRequest = requestFactory.ConstructNew(inputString);
                var imageComparer     = comparisonFactory.ConstructNew(comparisonRequest, initialOptions);

                List<DuplicateResult> duplicateResults;
                
                // For now, just doing a try/catch at the highest level. I plan
                // to have much better handling. I'll implement the `Either` monad
                // and have the `Run` method return an instance of that. From there,
                // I can determine whether to write an error out, or to write the results.
                try
                {
                    var sw = Stopwatch.StartNew();
                    duplicateResults = await imageComparer.Run(comparisonRequest);
                    sw.Stop();

                    Console.WriteLine($"Done in {sw.ElapsedMilliseconds} ms.");
                }
                catch (Exception e)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(e.Message);
                    Console.ForegroundColor = ConsoleColor.White;
                    
                    continue;
                }
                
                /* TODO
                 * Handle displaying the equality percentage in results.
                 * Always show this for single comparison
                 */
                if (duplicateResults.Count <= 0)
                    HandleNoDuplicates(inputString);
                else
                    HandleHasDuplicates(duplicateResults, inputString);
                
                // Force the garbage collector to run, after each search
                // session. The idea being that collector will clean up any
                // outstanding resources from the run.
                GC.Collect();
            } while (true);
        }

        
        static bool CommandIsGiven(string input, IEnumerable<string> toCheck)
        {
            return toCheck.Any(command => 
                input.Trim()
                    .ToLowerInvariant()
                    .Equals(command));
        }
        
        static void OutputHelpText()
        {
            Console.WriteLine("_____About_____");
            Console.WriteLine("This application is used to discover and determine duplicate images.");
            Console.WriteLine("This is done in 1 of 3 'request' types. Only 1 request can be active at a given time.");
            Console.WriteLine("The 3 request types are 'Directory', 'Single', and 'Pair'.");
            Console.WriteLine("Directory compares all images in a directory with every other one.");
            Console.WriteLine("Single compares 1 image with all other images in a given directory.");
            Console.WriteLine("Pair compares 2 different images with one another.");
            
            Console.WriteLine("_____Request Formats_____");
            Console.WriteLine("Requests can be made using the following formats"); 
            Console.WriteLine("    (Directory)              '/path/to/some/directory'");
            Console.WriteLine("    (Image with Directory)   '/path/to/image.[extension] , /directory/to/compare/against'");
            Console.WriteLine("    (Image with Other Image) '/path/to/image1.[extension] , /path/to/image2.[extension]'");
            Console.WriteLine("Valid extensions are " + validExtensionsCombined);
            Console.WriteLine("Other extension types will simply be ignore by the application.");
            
            Console.WriteLine("_____Options_____");
            Console.WriteLine("There are currently 2 options that can be set.");
            Console.WriteLine("Directory Level: Tells the program how deep in the directory to search. Does not apply to the Singe request type.");
            Console.WriteLine("    Values: all, [top]");
            Console.WriteLine("Bias Factor: The percentage that a comparison must equal, or exceed, for an image to be considered a duplicate.");
            Console.WriteLine("    Values: 0 to 100, [90]");
            Console.WriteLine("Type 'options' to overwrite the current option settings.");

        }
        
        /// <summary>
        /// Change the options that the programs performs comparisons with. The
        /// user will be prompted for each option to overwrite, one at a time.
        /// </summary>
        /// <param name="currentOptions">The options comparisons are currently using.</param>
        /// <returns>The new options future comparisons should run with.</returns>
        static ComparisonOptions OverwriteComparisonOptions(ComparisonOptions currentOptions)
        {
            Console.WriteLine("Enter New Option Values");
            Console.WriteLine("Leave an option blank to keep its current value.");
            var flagsToChange = new Dictionary<string, string>();
            
            // Ask for how deep to look in the directory. If the user does not input a value,
            // we keep the current setting.
            Console.WriteLine("Directory Level: ");
            var newDirectoryLevel = Console.ReadLine();
            if (!string.IsNullOrEmpty(newDirectoryLevel))
                flagsToChange[CommandFlagProperties.SearchOptionFlag.Name] = newDirectoryLevel;
            
            // Ask for the new bias factor. The current setting is kept if the user gives
            // no new value.
            Console.WriteLine("Bias Factor: ");
            var newBiasFactor = Console.ReadLine();
            if (!string.IsNullOrEmpty(newBiasFactor))
                flagsToChange[CommandFlagProperties.BiasFactorFlag.Name] = newBiasFactor;

            // Build a new `ComparisonOptions` object, using the newly created dictionary
            // from the user's input.
            var updatedOptions = new ComparisonOptionsBuilder()
                .FromCommandFlags(flagsToChange, new Some<ComparisonOptions>(currentOptions));

            return updatedOptions;
        }
        
        static void HandleNoDuplicates(string requestedDirectory)
        {
            Console.WriteLine($"No duplicate images were found in '{requestedDirectory}'.");
        }
        
        static void HandleHasDuplicates(List<DuplicateResult> duplicateResults, string requestedDirectory)
        {
            Console.WriteLine($"The following {duplicateResults.Count} duplicates were found in '{requestedDirectory}':");
            for (var resultIndex = 0; resultIndex < duplicateResults.Count(); resultIndex++)
            {
                if (!duplicateResults[resultIndex].Duplicates.Any())
                    continue;
                    
                Console.WriteLine($"Result #{resultIndex}: {duplicateResults[resultIndex].BaseImage.Name}");
                for (var dupIndex = 0; dupIndex < duplicateResults[resultIndex].Duplicates.Count(); dupIndex++)
                    Console.WriteLine($"\tDupe #{dupIndex}: {duplicateResults[resultIndex].Duplicates[dupIndex].Name}");
            }
        }
    }
}
