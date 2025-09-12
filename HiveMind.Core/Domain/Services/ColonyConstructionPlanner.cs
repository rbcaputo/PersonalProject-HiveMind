using HiveMind.Core.Domain.Aggregates;
using HiveMind.Core.Domain.Common;
using HiveMind.Core.Domain.Entities;
using HiveMind.Core.Domain.ValueObjects;

namespace HiveMind.Core.Domain.Services
{
  // ------------------------------------------------------------
  //  Domain service for collective construction decision making
  //  Implements swarm intelligence algorithms
  // ------------------------------------------------------------

  public class ColonyConstructionPlanner(Random? random = null)
  {
    private readonly Random _random = random ?? new();
    private readonly ConstructionDecisionMatrix _decisionMatrix = new();

    //  Analyzes colony needs and generates construction recommendations
    //  Uses collective intelligence algorithms
    public ConstructionRecommendation AnalyzeConstructionNeeds(
      NestArchitecture currentNest,
      ColonyNeeds needs,
      IEnumerable<Ant> availableWorkers
    )
    {
      var workerVotes = CollectWorkerVotes(availableWorkers, needs, currentNest);
      var urgencyAnalysis = AnalyzeUrgentNeeds(needs, currentNest);
      var resourceAnalysis = AnalyzeResourceAvailability(needs, availableWorkers);

      var recommendation = SynthesizeRecommendation(workerVotes, urgencyAnalysis, resourceAnalysis, currentNest);

      return recommendation;
    }

    //  Optimizes construction project scheduling based on worker availability and priorities
    public ConstructionSchedule OptimizeConstructionSchedule(
      IEnumerable<ConstructionProject> plannedProjects,
      IEnumerable<Ant> availableWorkers,
      ColonyResourceState resources
    )
    {
      var projects = plannedProjects.ToList();
      var workers = availableWorkers
        .OfType<WorkerAnt>()
        .ToList();

      //  Sort projects by priority and feasibility
      var prioritizedProjects = projects
        .Where(p => p.Status == ConstructionStatus.Planned)
        .OrderByDescending(p => CalculateProjectScore(p, resources))
        .ToList();

      ConstructionSchedule schedule = new();
      List<WorkerAnt> availableWorkerPool = [.. workers];

      foreach (var project in prioritizedProjects)
      {
        var suitableWorkers = FindSuitableWorkers(project, availableWorkerPool);

        if (suitableWorkers.Count >= project.RequiredWorkers)
        {
          WorkerAssignment assignment = new(project, suitableWorkers.Take(project.RequiredWorkers));
          schedule.AddAssignment(assignment);

          //  Remove assigned workers from available pool
          foreach (var assignedWorker in assignment.AssignedWorkers)
            availableWorkerPool.Remove(assignedWorker);
        }
        else
          //  Partial assignment or delay recommendation
          schedule.AddDelayedProject(project, $"Insufficient workers: need {project.RequiredWorkers}, have {suitableWorkers.Count}");
      }

      return schedule;
    }

    //  Evaluates construction site locations using swarm intelligence principles
    public LocationEvaluation EvaluateConstructionSite(
      Position proposedLocation,
      ChamberType chamberType,
      NestArchitecture existingNest,
      ColonyNeeds needs
    )
    {
      LocationEvaluation evaluation = new(proposedLocation, chamberType);

      //  Distance factors
      evaluation.AddFactor("Accessibility", CalculateAccessibilityScore(proposedLocation, existingNest));
      evaluation.AddFactor("Isolation", CalculateIsolationScore(proposedLocation, chamberType, existingNest));

      //  Environmental factors
      evaluation.AddFactor("Drainage", CalculateDrainageScore(proposedLocation));
      evaluation.AddFactor("Stability", CalculateStabilityScore(proposedLocation));
      evaluation.AddFactor("Protection", CalculateProtectionScore(proposedLocation, chamberType));

      //  Strategic factors
      evaluation.AddFactor("Strategic_Value", CalculateStrategicValue(proposedLocation, chamberType, needs));
      evaluation.AddFactor("Future_Expansion", CalculateFutureExpansionPotential(proposedLocation, existingNest));

      return evaluation;
    }

    // --------------------------------
    //  Private implementation methods
    // --------------------------------

    private Dictionary<ChamberType, VoteResult> CollectWorkerVotes(
      IEnumerable<Ant> workers,
      ColonyNeeds needs,
      NestArchitecture currentNest
    )
    {
      Dictionary<ChamberType, VoteResult> votes = [];

      foreach (var worker in workers.OfType<WorkerAnt>().Where(w => w.IsProductive))
      {
        var workerPreferences = GetWorkerPreferences(worker, needs, currentNest);

        foreach (var (chamberType, weight) in workerPreferences)
        {
          if (!votes.ContainsKey(chamberType))
            votes[chamberType] = new(chamberType);

          votes[chamberType].AddVote(worker.Id, weight, worker.ExperienceLevel);
        }
      }

      return votes;
    }

    private Dictionary<ChamberType, double> GetWorkerPreferences(
      WorkerAnt worker,
      ColonyNeeds needs,
      NestArchitecture currentNest
    )
    {
      Dictionary<ChamberType, double> preferences = [];

      //  Experience-based preferences
      foreach (var specialization in worker.Specializations)
      {
        var preferredChambers = GetPreferredChambersForSpecialization(specialization);
        foreach (var chamber in preferredChambers)
          preferences[chamber] = preferences.GetValueOrDefault(chamber, 0) +
          worker.GetSpecializationLevel(specialization) * 2.0;
      }

      //  Needs-based preferences (all workers respond to colony needs)
      if (needs.FoodStockLevel < 0.3)
        preferences[ChamberType.FoodStorage] = preferences.GetValueOrDefault(ChamberType.FoodStorage, 0) + 3.0;
      if (needs.PopulationGrowthRate > 0.1)
        preferences[ChamberType.Nursery] = preferences.GetValueOrDefault(ChamberType.Nursery, 0) + 2.5;
      if (needs.WasteAccumulation > 0.7)
        preferences[ChamberType.WasteDumps] = preferences.GetValueOrDefault(ChamberType.WasteDumps, 0) + 2.0;
      if (needs.DefenseThreatLevel > 0.5)
        preferences[ChamberType.Entrance] = preferences.GetValueOrDefault(ChamberType.Entrance, 0) + 1.8;

      return preferences;
    }

    private List<ChamberType> GetPreferredChambersForSpecialization(WorkerSpecialization specialization) =>
      specialization switch
      {
        WorkerSpecialization.Construction =>
          [ChamberType.WorkshopArea, ChamberType.MainTunnel],
        WorkerSpecialization.Nursing =>
          [ChamberType.Nursery, ChamberType.QueensChamber],
        WorkerSpecialization.Foraging =>
          [ChamberType.FoodStorage, ChamberType.Entrance],
        WorkerSpecialization.Maintenance =>
          [ChamberType.MainTunnel, ChamberType.EmergencyChamber],
        WorkerSpecialization.Defense =>
          [ChamberType.Entrance, ChamberType.EmergencyChamber],
        WorkerSpecialization.Excavation =>
          [ChamberType.WinterChamber, ChamberType.EmergencyChamber],
        WorkerSpecialization.FoodProcessing =>
          [ChamberType.FoodStorage, ChamberType.WorkshopArea],
        WorkerSpecialization.Ventilation =>
          [ChamberType.MainTunnel, ChamberType.Entrance],
        _ =>
          []
      };

    private UrgencyAnalysis AnalyzeUrgentNeeds(ColonyNeeds needs, NestArchitecture currentNest)
    {
      UrgencyAnalysis analysis = new();

      //  Critical needs (immediate action required)
      if (needs.FoodStockLevel < 0.1)
        analysis.AddCriticalNeed(ChamberType.FoodStorage, "Food crisis - colony survival at risk");
      if (needs.PopulationGrowthRate > 0.2 && currentNest.Nurseries.Count == 0)
        analysis.AddCriticalNeed(ChamberType.Nursery, "Population boom without brood care facilities");
      if (currentNest.HasStructuralIssues)
        analysis.AddCriticalNeed(ChamberType.EmergencyChamber, "Structural damage requires emergency shelter");

      //  High priority needs
      if (needs.DefenseThreatLevel > 0.8)
        analysis.AddHighPriorityNeed(ChamberType.Entrance, "High threat level requires improved defenses");
      if (needs.WasteAccumulation > 0.9)
        analysis.AddHighPriorityNeed(ChamberType.WasteDumps, "Waste crisis affecting colony health");
      if (needs.SpaceUtilization > 0.95)
        analysis.AddHighPriorityNeed(ChamberType.EmergencyChamber, "Severe overcrowding");

      return analysis;
    }

    private ResourceAnalysis AnalyzeResourceAvailability(ColonyNeeds needs, IEnumerable<Ant> workers)
    {
      ResourceAnalysis analysis = new();
      var workerAnts = workers
        .OfType<WorkerAnt>()
        .Where(w => w.IsProductive)
        .ToList();

      analysis.AvailableWorkers = workerAnts.Count;
      analysis.AverageWorkerExperience = workerAnts.Count > 0
        ? workerAnts.Average(w => w.ExperienceLevel)
        : 0;
      analysis.SpecializationCoverage = CalculateSpecializationCoverage(workerAnts);
      analysis.ConstructionCapacity = CalculateConstructionCapacity(workerAnts);
      analysis.EnergyReserves = needs.FoodStockLevel;  //  Food translates to energy for work

      return analysis;
    }

    private Dictionary<WorkerSpecialization, int> CalculateSpecializationCoverage(List<WorkerAnt> workers)
    {
      Dictionary<WorkerSpecialization, int> coverage = [];

      foreach (var specialization in Enum.GetValues<WorkerSpecialization>())
        coverage[specialization] = workers.Count(w => w.Specializations.Contains(specialization));

      return coverage;
    }

    private double CalculateConstructionCapacity(List<WorkerAnt> workers)
    {
      double capacity = 0;

      foreach (var worker in workers)
      {
        double workerCapacity = 1.0 + worker.ExperienceLevel * 0.3;

        if (worker.Specializations.Contains(WorkerSpecialization.Construction))
          workerCapacity *= 1.8;
        if (worker.Specializations.Contains(WorkerSpecialization.Excavation))
          workerCapacity *= 1.5;

        capacity += workerCapacity;
      }

      return capacity;
    }

    private ConstructionRecommendation SynthesizeRecommendation(
      Dictionary<ChamberType, VoteResult> workerVotes,
      UrgencyAnalysis urgencyAnalysis,
      ResourceAnalysis resourceAnalysis,
      NestArchitecture currentNest
    )
    {
      ConstructionRecommendation recommendation = new();

      //  Process critical needs first
      foreach (var criticalNeed in urgencyAnalysis.CriticalNeeds)
      {
        var project = CreateConstructionProject(criticalNeed.ChamberType, Priority.Critical, currentNest);
        if (project != null && CanExecuteProject(project, resourceAnalysis))
          recommendation.AddRecommendedProject(project, criticalNeed.Reason);
      }

      //  Process high priority needs
      foreach (var highPriorityNeed in urgencyAnalysis.HighPriorityNeeds)
      {
        var project = CreateConstructionProject(highPriorityNeed.ChamberType, Priority.High, currentNest);
        if (project != null && CanExecuteProject(project, resourceAnalysis))
          recommendation.AddRecommendedProject(project, highPriorityNeed.Reason);
      }

      //  Process worker votes for remaining capacity
      var remainingCapacity = resourceAnalysis.ConstructionCapacity - recommendation.TotalResourceRequirement;
      if (remainingCapacity > 10)  //  Threshold for additional projects
      {
        var topVotes = workerVotes.Values
          .OrderByDescending(v => v.WeightedScore)
          .Take(3);

        foreach (var vote in topVotes)
        {
          var project = CreateConstructionProject(vote.ChamberType, Priority.Medium, currentNest);
          if (project != null && project.TotalEnergyCost <= remainingCapacity)
          {
            recommendation.AddRecommendedProject(project, $"Worker consensus (score: {vote.WeightedScore:F1})");
            remainingCapacity -= project.TotalEnergyCost;
          }
        }
      }

      return recommendation;
    }

    private ConstructionProject? CreateConstructionProject(ChamberType chamberType, Priority priority, NestArchitecture currentNest)
    {
      //  Find optimal location for the chamber type
      var location = FindOptimalLocation(chamberType, currentNest);
      if (location == null)
        return null;

      //  Estimate work requirements
      var workDays = EstimateConstructionTime(chamberType);
      var workers = EstimateWorkerRequirement(chamberType);

      return new(chamberType, location.Value, workDays, workers, priority);
    }

    private Position? FindOptimalLocation(ChamberType chamberType, NestArchitecture currentNest)
    {
      var center = currentNest.CenterPosition;
      var existingChambers = currentNest.Chambers.Values;

      //  Generate candidate positions in a spiral pattern
      for (int radius = 10; radius <= 50; radius += 5)
        for (int angle = 0; angle < 360; angle += 30)
        {
          var radians = angle * Math.PI / 180.0;
          var candidate = new Position(
            center.X + Math.Cos(radians) * radius,
            center.Y + Math.Sin(radians) * radius
          );

          if (IsValidLocationForChamberType(candidate, chamberType, existingChambers))
            return candidate;
        }

      return null;  //  No suitable location found
    }

    private bool IsValidLocationForChamberType(Position location, ChamberType chamberType, IEnumerable<NestChamber> existingChambers)
    {
      //  Check minimum distances for different chamber types
      Dictionary<ChamberType, double> minDistances = new()
      {
        [ChamberType.WasteDumps] = 15.0,     //  Keep waste away from everything
        [ChamberType.QueensChamber] = 20.0,  //  Royal chamber needs space
        [ChamberType.Entrance] = 25.0,       //  Entrances need to be spread out
      };

      var requiredDistance = minDistances.GetValueOrDefault(chamberType, 8.0);

      return !existingChambers
        .Any(c => location.DistanceTo(c.CenterPosition) < requiredDistance);
    }

    private double EstimateConstructionTime(ChamberType chamberType) =>
      chamberType switch
      {
        ChamberType.QueensChamber =>
          15.0,  //  Complex royal chamber
        ChamberType.FoodStorage =>
          12.0,  //  Specialized storage
        ChamberType.WinterChamber =>
          18.0,  //  Deep, insulated chamber
        ChamberType.MainTunnel =>
          8.0,   //  Wide corridor
        ChamberType.Nursery =>
          10.0,  //  Specialized brood chamber
        ChamberType.WasteDumps =>
          6.0,   //  Simple waste pit
        ChamberType.Entrance =>
          7.0,   //  Security features
        ChamberType.WorkshopArea =>
          9.0,   //  Tool storage
        ChamberType.EmergencyChamber =>
          5.0,   //  Basic shelter
        ChamberType.FungusGarden =>
          14.0,  //  Complex cultivation chamber
        _ =>
          8.0    //  Default estimate
      };

    private int EstimateWorkerRequirement(ChamberType chamberType) =>
      chamberType switch
      {
        ChamberType.QueensChamber =>
          8,   //  Skilled royal construction
        ChamberType.FoodStorage =>
          6,   //  Specialized storage work
        ChamberType.WinterChamber =>
          10,  //  Deep excavation work
        ChamberType.MainTunnel =>
          12,  //  Wide excavation
        ChamberType.Nursery =>
          5,   //  Careful brood chamber work
        ChamberType.WasteDumps =>
          3,   //  Simple excavation
        ChamberType.Entrance =>
          4,   //  Security construction
        ChamberType.WorkshopArea =>
          6,   //  Organized construction
        ChamberType.EmergencyChamber =>
          4,   //  Basic excavation
        ChamberType.FungusGarden =>
          7,   //  Specialized cultivation setup
        _ =>
          5    //  Default requirement
      };

    private bool CanExecuteProject(ConstructionProject project, ResourceAnalysis resources) =>
      resources.AvailableWorkers >= project.RequiredWorkers &&
      resources.ConstructionCapacity >= project.TotalEnergyCost &&
      resources.EnergyReserves >= 0.2;  //  Need minimum energy reserves

    private double CalculateProjectScore(ConstructionProject project, ColonyResourceState resources)
    {
      double priorityScore = (double)project.Priority * 10.0;
      double feasibilityScore = Math.Min(10.0, resources.AvailableEnergy / project.TotalEnergyCost);
      double urgencyScore = project.PlannedAt < DateTime.UtcNow.AddDays(-7)
        ? 5.0
        : 0.0;

      return priorityScore + feasibilityScore + urgencyScore;
    }

    private List<WorkerAnt> FindSuitableWorkers(ConstructionProject project, List<WorkerAnt> availableWorkers)
    {
      var suitableWorkers = availableWorkers
        .Where(w => w.IsProductive)
        .OrderByDescending(w => CalculateWorkerSuitability(w, project))
        .ToList();

      return suitableWorkers;
    }

    private double CalculateWorkerSuitability(WorkerAnt worker, ConstructionProject project)
    {
      double suitability = worker.ExperienceLevel;

      //  Specialization bonuses
      if (worker.Specializations.Contains(WorkerSpecialization.Construction))
        suitability += 3.0;

      if (worker.Specializations.Contains(WorkerSpecialization.Excavation))
        suitability += 2.0;

      //  Chamber-specific bonuses
      var chamberSpecialization = GetRequiredSpecializationForChamber(project.TargetType);
      if (chamberSpecialization.HasValue && worker.Specializations.Contains(chamberSpecialization.Value))
        suitability += 2.5;

      return suitability;
    }

    private WorkerSpecialization? GetRequiredSpecializationForChamber(ChamberType chamberType) =>
      chamberType switch
      {
        ChamberType.Nursery =>
          WorkerSpecialization.Nursing,
        ChamberType.FoodStorage =>
          WorkerSpecialization.FoodProcessing,
        ChamberType.WasteDumps =>
          WorkerSpecialization.Maintenance,
        ChamberType.MainTunnel =>
          WorkerSpecialization.Ventilation,
        ChamberType.Entrance =>
          WorkerSpecialization.Defense,
        ChamberType.WorkshopArea =>
          WorkerSpecialization.Construction,
        _ =>
          null
      };

    //  Location evaluation helper methods
    private double CalculateAccessibilityScore(Position location, NestArchitecture nest)
    {
      if (!nest.Chambers.Any())
        return 1.0;

      var avgDistance = nest.Chambers.Values
        .Average(c => location.DistanceTo(c.CenterPosition));

      //  Closer to existing chambers is generally better (but not too close)
      return Math.Max(0.1, Math.Min(1.0, 30.0 / avgDistance));
    }

    private double CalculateIsolationScore(Position location, ChamberType chamberType, NestArchitecture nest)
    {
      //  Some chamber types benefit from isolation
      var isolationPreference = chamberType switch
      {
        ChamberType.WasteDumps =>
          1.0,  //  High isolation preferred
        ChamberType.QueensChamber =>
          0.7,  //  Moderate isolation
        ChamberType.WinterChamber =>
          0.8,  //  High isolation for quiet
        _ =>
          0.2   //  Low isolation preferred
      };

      if (!nest.Chambers.Any())
        return isolationPreference;

      var nearestDistance = nest.Chambers.Values
        .Min(c => location.DistanceTo(c.CenterPosition));

      var isolationScore = Math.Min(1.0, nearestDistance / 20.0);

      //  Weight by preference
      return isolationScore * isolationPreference + (1.0 - isolationPreference) * (1.0 - isolationScore);
    }

    private double CalculateStrategicValue(Position location, ChamberType chamberType, ColonyNeeds needs)
    {
      double value = 0.5;  //  Base value

      //  Strategic positioning bonuses based on needs
      if (chamberType == ChamberType.FoodStorage && needs.FoodStockLevel < 0.3)
        value += 0.4;
      if (chamberType == ChamberType.Nursery && needs.PopulationGrowthRate > 0.1)
        value += 0.3;
      if (chamberType == ChamberType.Entrance && needs.DefenseThreatLevel > 0.5)
        value += 0.3;

      return Math.Min(1.0, value);
    }

    private double CalculateDrainageScore(Position location)
    {
      //  Simplified drainage calculation - in reality would use terrain data
      //  Higher positions generally have better drainage
      return Math.Min(1.0, location.Y / 100.0 + 0.3);
    }

    private double CalculateStabilityScore(Position location)
    {
      //  Simplified stability calculation
      //  In reality would analyze soil composition and geological factors
      var distanceFromEdge = Math.Min(location.X, location.Y);
      return Math.Min(1.0, distanceFromEdge / 20.0 + 0.2);
    }

    private double CalculateProtectionScore(Position location, ChamberType chamberType)
    {
      //  Different chambers need different levels of protection
      var protectionNeed = chamberType switch
      {
        ChamberType.QueensChamber =>
          1.0,  //  Maximum protection
        ChamberType.Nursery =>
          0.9,  //  High protection for brood
        ChamberType.FoodStorage =>
          0.7,  //  Good protection for resources
        ChamberType.WinterChamber =>
          0.8,  //  Protected from weather
        ChamberType.Entrance =>
          0.3,  //  Needs to be accessible
        _ =>
          0.5   //  Moderate protection
      };

      //  Simple protection score based on distance from edges
      var edgeDistance = Math.Min(location.X, location.Y);
      var protectionScore = Math.Min(1.0, edgeDistance / 25.0);

      return protectionScore * protectionNeed + (1.0 - protectionNeed) * 0.5;
    }

    private double CalculateFutureExpansionPotential(Position location, NestArchitecture nest)
    {
      //  Check how much space is available around this location for future growth
      int openSpaceCount = 0;
      const double checkRadius = 15.0;

      for (int angle = 0; angle < 360; angle += 45)
      {
        var radians = angle * Math.PI / 180.0;
        Position checkPosition = new(
          location.X + Math.Cos(radians) * checkRadius,
          location.Y + Math.Sin(radians) * checkRadius
        );

        bool spaceAvailable = !nest.Chambers.Values
          .Any(c => checkPosition.DistanceTo(c.CenterPosition) < 8.0);

        if (spaceAvailable)
          openSpaceCount++;
      }

      return openSpaceCount / 8.0;  //  8 directions checked
    }
  }

  // --------------------------------------------------
  //  Supporting classes for construction intelligence
  // --------------------------------------------------

  public class VoteResult(ChamberType chamberType)
  {
    public ChamberType ChamberType { get; } = chamberType;
    public List<WorkerVote> Votes { get; } = [];
    public double WeightedScore =>
      Votes.Sum(v => v.Weight * (1.0 + v.WorkerExperience));
    public int VoteCount =>
      Votes.Count;

    public void AddVote(Guid workerId, double weight, double workerExperience) =>
      Votes.Add(new WorkerVote(workerId, weight, workerExperience));


  }

  public record WorkerVote(Guid WorkerId, double Weight, double WorkerExperience);

  public class UrgencyAnalysis
  {
    public List<UrgentNeed> CriticalNeeds { get; } = [];
    public List<UrgentNeed> HighPriorityNeeds { get; } = [];

    public void AddCriticalNeed(ChamberType chamberType, string reason) =>
      CriticalNeeds.Add(new(chamberType, reason));

    public void AddHighPriorityNeed(ChamberType chamberType, string reason) =>
      HighPriorityNeeds.Add(new(chamberType, reason));
  }

  public record UrgentNeed(ChamberType ChamberType, string Reason);

  public class ResourceAnalysis
  {
    public int AvailableWorkers { get; set; }
    public double AverageWorkerExperience { get; set; }
    public Dictionary<WorkerSpecialization, int> SpecializationCoverage { get; set; } = [];
    public double ConstructionCapacity { get; set; }
    public double EnergyReserves { get; set; }
  }

  public class ConstructionRecommendation
  {
    public List<RecommendedProject> RecommendedProjects { get; } = [];
    public double TotalResourceRequirement =>
      RecommendedProjects.Sum(p => p.Project.TotalEnergyCost);

    public void AddRecommendedProject(ConstructionProject project, string reasoning) =>
      RecommendedProjects.Add(new(project, reasoning));
  }

  public record RecommendedProject(ConstructionProject Project, string Reasoning);

  public class ConstructionSchedule
  {
    public List<WorkerAssignment> ActiveAssignments { get; } = [];
    public List<DelayedProject> DelayedProjects { get; } = [];

    public void AddAssignment(WorkerAssignment assignment) =>
      ActiveAssignments.Add(assignment);

    public void AddDelayedProject(ConstructionProject project, string reason) =>
      DelayedProjects.Add(new DelayedProject(project, reason));
  }

  public class WorkerAssignment(ConstructionProject project, IEnumerable<WorkerAnt> assignedWorkers)
  {
    public ConstructionProject Project { get; } = project;
    public IReadOnlyList<WorkerAnt> AssignedWorkers { get; } = assignedWorkers.ToList().AsReadOnly();
    public DateTime AssignedAt { get; } = DateTime.UtcNow;
    public double EstimatedCompletion =>
      Project.EstimatedWorkDays;
  }

  public record DelayedProject(ConstructionProject Project, string DelayReason);

  public class LocationEvaluation(Position location, ChamberType chamberType)
  {
    public Position Location { get; } = location;
    public ChamberType ChamberType { get; } = chamberType;
    public Dictionary<string, double> EvaluationFactors { get; } = [];
    public double OverallScore =>
      EvaluationFactors.Values.Average();

    public void AddFactor(string factorName, double score) =>
      EvaluationFactors[factorName] = Math.Max(0.0, Math.Min(1.0, score));

    public bool IsRecommendedLocation =>
      OverallScore >= 0.7;
    public string GetScoreBreakdown() =>
      string.Join(", ", EvaluationFactors.Select(kv => $"{kv.Key}: {kv.Value:F2}"));
  }

  public class ColonyResourceState
  {
    public double AvailableEnergy { get; set; }
    public double FoodReserves { get; set; }
    public int TotalWorkers { get; set; }
    public int AvailableWorkers { get; set; }
    public Dictionary<WorkerSpecialization, int> SpecializationCounts { get; set; } = [];
    public double ConstructionMaterialReserves { get; set; }
  }

  public class ConstructionDecisionMatrix
  {
    private readonly Dictionary<ChamberType, ConstructionFactors> _factorMatrix;

    public ConstructionDecisionMatrix() =>
      _factorMatrix = InitializeDecisionMatrix();

    public ConstructionFactors GetFactors(ChamberType chamberType) =>
      _factorMatrix.GetValueOrDefault(chamberType, new());

    private Dictionary<ChamberType, ConstructionFactors> InitializeDecisionMatrix() =>
      new()
      {
        [ChamberType.QueensChamber] = new()
        {
          UrgencyMultiplier = 2.0,
          ResourcePriority = 0.9,
          PopulationThreshold = 0.05,
          OptimalTiming = "Early establishment"
        },
        [ChamberType.Nursery] = new()
        {
          UrgencyMultiplier = 1.8,
          ResourcePriority = 0.8,
          PopulationThreshold = 0.1,
          OptimalTiming = "Population growth phase"
        },
        [ChamberType.FoodStorage] = new()
        {
          UrgencyMultiplier = 1.5,
          ResourcePriority = 0.7,
          PopulationThreshold = 0.2,
          OptimalTiming = "Resource scarcity"
        },
        [ChamberType.Entrance] = new()
        {
          UrgencyMultiplier = 1.3,
          ResourcePriority = 0.6,
          PopulationThreshold = 0.3,
          OptimalTiming = "Threat detection"
        },
        [ChamberType.WasteDumps] = new()
        {
          UrgencyMultiplier = 1.0,
          ResourcePriority = 0.4,
          PopulationThreshold = 0.5,
          OptimalTiming = "Sanitation crisis"
        }
      };
  }

  public class ConstructionFactors
  {
    public double UrgencyMultiplier { get; set; } = 1.0;
    public double ResourcePriority { get; set; } = 0.5;
    public double PopulationThreshold { get; set; } = 0.3;
    public string OptimalTiming { get; set; } = "Standard development";
  }

  // ------------------------------------------------------------------------
  //  Represents the colony's current needs for construction decision making
  // ------------------------------------------------------------------------

  public class ColonyNeeds
  {
    public double PopulationGrowthRate { get; set; }  //  Rate of population increase
    public double FoodStockLevel { get; set; }        //  Current food reserves (0-1)
    public double DefenseThreatLevel { get; set; }    //  Perceived threat level (0-1)
    public double WasteAccumulation { get; set; }     //  Waste buildup (0-1)
    public int EstimatedPopulation { get; set; }      //  Current estimated population
    public double SpaceUtilization { get; set; }      //  How full the nest is (0-1)
    public double EnvironmentalStress { get; set; }   //  Environmental pressure (0-1)
    public double ResourceAvailability { get; set; }  //  Available construction resources (0-1)

    //  Gets overall colony stress level for urgent construction needs
    public double OverallStressLevel =>
      (FoodStockLevel < 0.3 ? 0.4 : 0) +
      (DefenseThreatLevel * 0.3) +
      (WasteAccumulation > 0.8 ? 0.2 : 0) +
      (SpaceUtilization > 0.9 ? 0.1 : 0);

    //  Indicates if colony is in crisis mode requiring emergency construction
    public bool IsInCrisisMode =>
      OverallStressLevel > 0.7;

    //  Gets construction urgency score for prioritization
    public double GetConstructionUrgency() =>
      Math.Min(1.0, OverallStressLevel + (PopulationGrowthRate * 0.5));
  }

  public record ConstructionPriority(ChamberType ChamberType, Priority Priority, string Reason);
}
