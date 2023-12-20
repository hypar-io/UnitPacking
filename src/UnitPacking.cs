using Elements;
using Elements.Geometry;
using System.Collections.Generic;
using System.Text;

namespace UnitPacking
{
  public static class UnitPacking
  {
    /// <summary>
    /// The UnitPacking function.
    /// </summary>
    /// <param name="model">The input model.</param>
    /// <param name="input">The arguments to the execution.</param>
    /// <returns>A UnitPackingOutputs instance containing computed results and the model with any new elements.</returns>
    public static UnitPackingOutputs Execute(Dictionary<string, Model> inputModels, UnitPackingInputs input)
    {
      // Your code here.
      var output = new UnitPackingOutputs();

      var units = input.UnitDefinitions;
      if (units == null || units.Count == 0)
      {
        output.Errors.Add("No units provided. Please provide at least one unit.");
        return output;
      }

      if (units.Select(x => x.Width).Contains(0))
      {
        output.Errors.Add("One or more units have a width of 0. Please provide a width greater than 0.");
        return output;
      }

      var buildingSegment = input.BuildingSegment;

      if (buildingSegment == null)
      {
        output.Errors.Add("No building segment provided. Please draw a line that represents your building segment.");
        return output;
      }

      var buildingLength = buildingSegment.Length();
      var lengthTolerance = input.LengthTolerance;

      var availableModuleWidths = units.Select(x => x.Width).Distinct().ToList();

      // This can be set to filter out the number of unique widths that can be included in each solution.
      var maxOfUniqueGrids = availableModuleWidths.Count;

      List<Dictionary<double, int>> packingSolutions = GetPackingSolutionsForBuildingSegment(lengthTolerance, maxOfUniqueGrids, availableModuleWidths, buildingSegment);

      if (packingSolutions.Count == 0)
      {
        output.Errors.Add("No solutions found. Try increasing your tolerance");
        return output;
      }

      var solutions = new List<PackingResult>();

      int solutionNumber = 1;

      foreach (var solution in packingSolutions)
      {
        var solutionRemainder = buildingLength - solution.Select(x => x.Key * x.Value).Sum();
        var result = new PackingResult(solutionNumber, ConvertDictionaryToString(solution), solutionRemainder);
        solutions.Add(result);
        solutionNumber++;
      }

      var selectedIndex = (int)Math.Min(input.SelectedSolution - 1, solutions.Count - 1);
      var selectedSolution = solutions[selectedIndex];

      var rectangles = new List<Polygon>();


      var materialDictionary = new Dictionary<double, Material>();

      foreach (var unit in units)
      {
        var material = new Material(unit.Width.ToString(), unit.Color.Value);
        materialDictionary.Add(unit.Width, material);
      }

      var buildingSegmentVector = buildingSegment.Direction();
      var initialRotation = new Transform(Vector3.Origin, Vector3.XAxis.PlaneAngleTo(buildingSegmentVector));
      var initialTransform = initialRotation.Concatenated(new Transform(buildingSegment.Start));

      var outlineMaterial = new Material("Outline", new Color(0.0, 0.0, 0.0, 0)) { EdgeDisplaySettings = new EdgeDisplaySettings() { LineWidth = 2.0 } };

      foreach (var pair in packingSolutions[selectedIndex])
      {
        for (int i = 0; i < pair.Value; i++)
        {
          var rectangle = (Polygon)Polygon.Rectangle(pair.Key, 10).Transformed(new Transform(pair.Key / 2, 5, 0));
          var panel = new Panel(rectangle, materialDictionary[pair.Key], initialTransform) { Name = pair.Key.ToString() };
          var outline = new ModelCurve(rectangle, outlineMaterial, initialTransform);
          output.Model.AddElement(panel);
          output.Model.AddElement(outline);
          initialTransform = initialTransform.Moved(buildingSegmentVector * pair.Key);
        }
      }

      var remainder = selectedSolution.Remainder;

      if (remainder > 0)
      {
        var rectangle = (Polygon)Polygon.Rectangle(remainder, 10).Transformed(new Transform(remainder / 2, 5, 0));
        var panel = new Panel(rectangle, BuiltInMaterials.XAxis, initialTransform) { Name = "Remainder" };
        var outline = new ModelCurve(rectangle, outlineMaterial, initialTransform);
        output.Model.AddElement(panel);
        output.Model.AddElement(outline);
      }

      output.Model.AddElements(solutions);
      output.NumberOfSolutions = solutions.Count;

      return output;
    }

    private static string ConvertDictionaryToString(Dictionary<double, int> dictionary)
    {
      var stringBuilder = new StringBuilder();
      foreach (var pair in dictionary)
      {
        if (stringBuilder.Length > 0)
        {
          stringBuilder.Append("; ");
        }
        stringBuilder.Append($"({pair.Key},{pair.Value})");
      }
      return stringBuilder.ToString();
    }

    private static List<Dictionary<double, int>> GetPackingSolutionsForBuildingSegment(double lengthTolerance, int maxNumberOfUniqueGrids, List<double> availableUnitWidths, Line buildingSegment)
    {
      var results = new List<Tuple<List<double>, double>>();
      var matchingLists = new List<Tuple<List<double>, double>>();
      var listOfAvailableWidths = new List<Dictionary<double, int>>();

      double buildingSegmentLength = buildingSegment.Length();

      if (availableUnitWidths.Count > 0)
      {
        FindWidthCombinations(availableUnitWidths, buildingSegmentLength, lengthTolerance, 0, 0, new List<double>(), results);

        var orderedResults = results.OrderBy(x => x.Item2).ToList();

        foreach (var resultItem in orderedResults)
        {
          var sortedList = resultItem.Item1
          .GroupBy(i => i)
          .OrderByDescending(grp => grp.Count()) // Sort by count
          .ThenBy(grp => grp.Key) // Then by value
          .SelectMany(grp => grp)
          .Distinct()
          .ToList();
          bool listMatches = true;

          if (sortedList.Count > maxNumberOfUniqueGrids + 1)
          {
            listMatches = false;
          }

          if (listMatches)
          {
            matchingLists.Add(new Tuple<List<double>, double>(resultItem.Item1, resultItem.Item2));
          }
        }

        foreach (var matchingList in matchingLists)
        {
          var availableWidthsTemp = new Dictionary<double, int>();

          foreach (var width in matchingList.Item1)
          {
            if (availableWidthsTemp.ContainsKey(width))
            {
              availableWidthsTemp[width] += 1;
            }
            else
            {
              availableWidthsTemp.Add(width, 1);
            }
          }

          listOfAvailableWidths.Add(availableWidthsTemp);
        }
      }

      var uniqueAvailableWidths = listOfAvailableWidths.Distinct().ToList();
      return uniqueAvailableWidths;
    }
    public static void FindWidthCombinations(List<double> values, double totalValue, double tolerance, int start, double currentSum, List<double> currentCombination, List<Tuple<List<double>, double>> results)
    {
      double remainder = totalValue - currentSum;

      if (remainder >= 0 && remainder <= tolerance)
      {
        results.Add(new Tuple<List<double>, double>(new List<double>(currentCombination), remainder));
        return;
      }

      if (currentSum > totalValue)
      {
        return;
      }

      for (int i = start; i < values.Count; i++)
      {
        currentCombination.Add(values[i]);
        FindWidthCombinations(values, totalValue, tolerance, i, currentSum + values[i], currentCombination, results);
        currentCombination.RemoveAt(currentCombination.Count - 1);
      }
    }
  }
}