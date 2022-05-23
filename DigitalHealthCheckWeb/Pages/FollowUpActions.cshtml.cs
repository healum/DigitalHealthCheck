using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DigitalHealthCheckCommon;
using DigitalHealthCheckEF;
using DigitalHealthCheckWeb.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DigitalHealthCheckWeb.Pages
{
    public class FollowUpActionsModel : HealthCheckPageModel
    {
        public class UnsanitisedModel
        {
            public IEnumerable<string> Alcohol { get; set; }

            public IEnumerable<string> BloodPressure { get; set; }

            public IEnumerable<string> BloodSugar { get; set; }

            public IEnumerable<string> Cholesterol { get; set; }

            public IEnumerable<string> GP { get; set; }

            public IEnumerable<string> ImproveBloodPressure { get; set; }

            public IEnumerable<string> ImproveBloodSugar { get; set; }

            public IEnumerable<string> ImproveCholesterol { get; set; }

            public IEnumerable<string> Mental { get; set; }

            public IEnumerable<string> Move { get; set; }

            public IEnumerable<string> Smoking { get; set; }

            public IEnumerable<string> Weight { get; set; }
        }

        private class SanitisedModel
        {
            public IEnumerable<Intervention> Alcohol { get; set; }

            public IEnumerable<Intervention> BloodPressure { get; set; }

            public IEnumerable<Intervention> BloodSugar { get; set; }

            public IEnumerable<Intervention> Cholesterol { get; set; }

            public IEnumerable<Intervention> GP { get; set; }

            public IEnumerable<Intervention> ImproveBloodPressure { get; set; }

            public IEnumerable<Intervention> ImproveBloodSugar { get; set; }

            public IEnumerable<Intervention> ImproveCholesterol { get; set; }

            public IEnumerable<Intervention> Mental { get; set; }

            public IEnumerable<Intervention> Move { get; set; }

            public IEnumerable<Intervention> Smoking { get; set; }

            public IEnumerable<Intervention> Weight { get; set; }
        }

        private readonly IBodyMassIndexCalculator bodyMassIndexCalculator;
        private readonly IHealthCheckResultFactory healthCheckResultFactory;
        private readonly IEveryoneHealthReferralService everyoneHealthReferralService;

        public IList<Intervention> AllInterventions { get; set; }

        public IList<CustomBarrier> CustomBarriers { get; set; }

        public IList<int> SelectedInterventions { get; set; }

        public IList<ActionCategory> ActionCategories { get; set; }

        public FollowUpActionsModel(
            Database database,
            ICredentialsDecrypter credentialsDecrypter,
            IBodyMassIndexCalculator bodyMassIndexCalculator,
            IPageFlow pageFlow,
            IHealthCheckResultFactory healthCheckResultFactory,
            IEveryoneHealthReferralService everyoneHealthReferralService)
            : base(database, credentialsDecrypter, pageFlow)
        {
            this.bodyMassIndexCalculator = bodyMassIndexCalculator;
            this.healthCheckResultFactory = healthCheckResultFactory;
            this.everyoneHealthReferralService = everyoneHealthReferralService;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            /*  if (!IsValidated())
              {
                  return RedirectToValidation();
              }*/
            ActionCategories = new List<ActionCategory>();
            AllInterventions = new List<Intervention>();

            var d = await LoadData();

            if (d != null)
            {
                return d;
            }
            // GenerateActionCategories();

       

            if (!AllInterventions.Any())
            {
                var check = await GetHealthCheckAsync();

                var result = healthCheckResultFactory.GetResult(check, false);

                if (result.Alcohol == DefaultStatus.Healthy &&
                    (result.BloodPressure is null || result.BloodPressure == BloodPressureStatus.Healthy) &&
                    (result.BloodSugar is null || result.BloodSugar == BloodSugarStatus.Healthy) &&
                    result.BodyMassIndex == BodyMassIndexStatus.Healthy &&
                    (result.Cholesterol is null || result.Cholesterol == DefaultStatus.Healthy) &&
                    result.Diabetes == DefaultStatus.Healthy &&
                    result.HeartAge == DefaultStatus.Healthy &&
                    result.HeartDisease == DefaultStatus.Healthy &&
                    result.PhysicalActivity == PhysicalActivityStatus.Active &&
                    result.Smoker == DefaultStatus.Healthy)
                {
                    //No interventions necessary, go straight to complete.

                    return RedirectWithId("./HealthCheckComplete");
                }
            }
            Console.WriteLine("action categories and all inteventions" + ActionCategories.Count() + ", " + AllInterventions.Count());
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(UnsanitisedModel actions)
        {
            var sanitisedActions = await ValidateAndSanitiseAsync(actions);

            if (sanitisedActions == null)
            {
                await LoadData();

                return await Reload();
            }

            var check = await GetHealthCheckAsync();

            check.ChosenInterventions = (sanitisedActions.Alcohol ?? Enumerable.Empty<Intervention>())
                .Concat(sanitisedActions.Move ?? Enumerable.Empty<Intervention>())
                .Concat(sanitisedActions.Smoking ?? Enumerable.Empty<Intervention>())
                .Concat(sanitisedActions.Weight ?? Enumerable.Empty<Intervention>())
                .Concat(sanitisedActions.GP ?? Enumerable.Empty<Intervention>())
                .Concat(sanitisedActions.Mental ?? Enumerable.Empty<Intervention>())
                .Concat(sanitisedActions.BloodPressure ?? Enumerable.Empty<Intervention>())
                .Concat(sanitisedActions.BloodSugar ?? Enumerable.Empty<Intervention>())
                .Concat(sanitisedActions.Cholesterol ?? Enumerable.Empty<Intervention>())
                .Concat(sanitisedActions.ImproveBloodPressure ?? Enumerable.Empty<Intervention>())
                .Concat(sanitisedActions.ImproveBloodSugar ?? Enumerable.Empty<Intervention>())
                .Concat(sanitisedActions.ImproveCholesterol ?? Enumerable.Empty<Intervention>())
                .ToList();

            await Database.SaveChangesAsync();

            if (everyoneHealthReferralService.HasEveryoneHealthReferrals(check))
            {
                return RedirectWithId("./EveryoneHealthConsent");
            }

            return RedirectWithId("./HealthCheckComplete");
        }

        protected override async Task<HealthCheck> GetHealthCheckAsync()
        {
            var check = await base.GetHealthCheckAsync();

            await Database.Entry(check).Collection(c => c.ChosenInterventions).LoadAsync();

            return check;
        }

        private async Task<IActionResult> LoadData()
        {

            var check = await GetHealthCheckAsync();

            await Database.Entry(check).Collection(c => c.ChosenBarriers).LoadAsync();

            await Database.Entry(check).Collection(c => c.CustomBarriers).LoadAsync();

            CustomBarriers = check.CustomBarriers.ToList();

            var barriers = check.ChosenBarriers;

            if (!barriers.Any())
            {
                // If no barriers have been selected and the patient has any health risks We show
                // the interventions for those risks

                var result = healthCheckResultFactory.GetResult(check, false);

                void LoadBarriers(string category)
                {
                    foreach (var barrier in Database.Barriers.Where(x => x.Category == category))
                    {
                        barriers.Add(barrier);
                    }
                }

                if (result.Alcohol != DefaultStatus.Healthy)
                {
                    LoadBarriers("alcohol");
                }

                if (result.BloodPressure is null)
                {
                    LoadBarriers("bloodpressure");
                }
                else if (result.BloodPressure is not null && result.BloodPressure != BloodPressureStatus.Healthy)
                {
                    LoadBarriers("improvebloodpressure");
                }

                if (result.BloodSugar is null)
                {
                    LoadBarriers("bloodsugar");
                }
                else if (result.BloodSugar is not null && result.BloodSugar != BloodSugarStatus.Healthy)
                {
                    LoadBarriers("improvebloodsugar");
                }

                if (result.BodyMassIndex != BodyMassIndexStatus.Healthy)
                {
                    LoadBarriers("weight");
                }

                if (result.Cholesterol is null)
                {
                    LoadBarriers("cholesterol");
                }
                else if (result.Cholesterol is not null && result.Cholesterol != DefaultStatus.Healthy)
                {
                    LoadBarriers("improvecholesterol");
                }

                if (result.PhysicalActivity != PhysicalActivityStatus.Active)
                {
                    LoadBarriers("move");
                }

                if (result.Smoker != DefaultStatus.Healthy)
                {
                    LoadBarriers("smoking");
                }
            }

            foreach (var barrier in barriers)
            {
                await Database.Entry(barrier).Collection(c => c.Interventions).LoadAsync();
            }

            //Some interventions appear regardless of barriers, but we only want to
            //show interventions for the categories of barriers people have chosen.

            var additionalInterventions = Database.Interventions.Where(x => x.Barrier == null && barriers.Select(y => y.Category).Contains(x.Category));

            AllInterventions = barriers
                       .SelectMany(x => x.Interventions)
                       .Concat(additionalInterventions)
                       .ToList();
            SelectedInterventions = AllInterventions.Select(x => x.Id).ToList();
            ActionCategories = await GenerateActionCategories();

            if (ActionCategories != null && ActionCategories.Count() > 0)
            {
                Console.WriteLine(AllInterventions.Count().ToString() + " all interventions");
                foreach (var intervention in AllInterventions)
                {
                    Console.WriteLine(intervention.Text.ToString() + " " + intervention.LinkDescription.ToString() + " " + intervention.Category);

                }



                foreach (var action in ActionCategories)
                {
                    Console.WriteLine("action is " + action.Name.ToString() + " " + action.Items.Count() + " items");
                }
                return await Reload();
            }


            return null;


   /*         Console.WriteLine(SelectedInterventions.Count.ToString() + " selected interventions");
            if (check != null && check.Height != null && check.Weight != null)
            {
                var bmi = bodyMassIndexCalculator.CalculateBodyMassIndex(check.Height.Value, check.Weight.Value);
                if (bmi >= 40)
                {
                    additionalInterventions = additionalInterventions.Where(x => x.Id != Database.BmiOver30InterventionId);
                }
                else if (bmi > 30)
                {
                    additionalInterventions = additionalInterventions.Where(x => x.Id != Database.BmiOver40InterventionId);
                }
                else
                {
                    additionalInterventions = additionalInterventions.Where(x => x.Id != Database.BmiOver30InterventionId && x.Id != Database.BmiOver40InterventionId);
                }

                if (check.BloodSugar < 42 || check.BloodSugar > 47)
                {
                    additionalInterventions = additionalInterventions.Where(x => x.Id != Database.NationalDiabetesPreventionProgramInterventionId);
                }
                // TODO Update interventions


            }

*/
        }

        private Task<List<ActionCategory>> GenerateActionCategories()
        {
            return Task.Run(() =>
                     new[] {
                    new ActionCategory
                    (
                        "Visit your GP clinic",
                        "Your GP clinic will discuss your results with you and make sure that you have all of the right follow-up clinical support you need, as well as access to lifestyle support services.",
                        "How would you like to be supported to visit your GP clinic? (optional)",
                        "GP",
                        AllInterventions.Where(x=> x.Category == "gp").ToList(),
                        SelectedInterventions.ToList(),
                        CustomBarriers.FirstOrDefault(x=> x.Category == "gp")?.Text
                    ),
                    new ActionCategory
                    (
                        "Stop smoking",
                        "Quitting smoking will help you live a healthier life.",
                        "How would you like to be supported to stop smoking? (optional)",
                        "Smoking",
                        AllInterventions.Where(x=> x.Category == "smoking").ToList(),
                        SelectedInterventions.ToList(),
                        CustomBarriers.FirstOrDefault(x=> x.Category == "smoking")?.Text
                    ),
                    new ActionCategory
                    (
                        "Achieve a healthy weight",
                        "Being a healthy weight is an important part of living well.",
                        "How would you like to be supported to achieve a healthy weight? (optional)",
                        "Weight",
                        AllInterventions.Where(x=> x.Category == "weight").ToList(),
                        SelectedInterventions.ToList(),
                        CustomBarriers.FirstOrDefault(x=> x.Category == "weight")?.Text
                    ),
                    new ActionCategory
                    (
                        "Drink less",
                        "Drinking less will help you feel better and be healthier.",
                        "How would you like to be supported to drink less? (optional)",
                        "Alcohol",
                        AllInterventions.Where(x=> x.Category == "alcohol").ToList(),
                        SelectedInterventions.ToList(),
                        CustomBarriers.FirstOrDefault(x=> x.Category == "alcohol")?.Text
                    ),
                    new ActionCategory
                    (
                        "Move more",
                        "Regular physical activity is an important part of living well.",
                        "How would you like to be supported to move more? (optional)",
                        "Move",
                        AllInterventions.Where(x=> x.Category == "move").ToList(),
                        SelectedInterventions.ToList(),
                        CustomBarriers.FirstOrDefault(x=> x.Category == "move")?.Text
                    ),
                    new ActionCategory
                    (
                        "Improve your mental wellbeing",
                        "Taking care of your mental wellbeing will help you to live a healthier life.",
                        "How would you like to be supported to improve your mental wellbeing? (optional)",
                        "Mental",
                        AllInterventions.Where(x=> x.Category == "mental").ToList(),
                        SelectedInterventions.ToList(),
                        CustomBarriers.FirstOrDefault(x=> x.Category == "mental")?.Text
                    ),
                    new ActionCategory
                    (
                        "Improve your cholesterol levels",
                        "Healthy cholesterol levels are an important part of living well.",
                        "How would you like to be supported to improve your cholesterol levels? (optional)",
                        "ImproveCholesterol",
                        AllInterventions.Where(x=> x.Category == "improvecholesterol").ToList(),
                        SelectedInterventions.ToList(),
                        CustomBarriers.FirstOrDefault(x=> x.Category == "improvecholesterol")?.Text
                    ),
                    new ActionCategory
                    (
                        "Improve your blood pressure",
                        "Healthy blood pressure is an important part of living well.",
                        "How would you like to be supported to improve your blood pressure levels? (optional)",
                        "ImproveBloodPressure",
                        AllInterventions.Where(x=> x.Category == "improvebloodpressure").ToList(),
                        SelectedInterventions.ToList(),
                        CustomBarriers.FirstOrDefault(x=> x.Category == "improvebloodpressure")?.Text
                    ),
                    new ActionCategory
                    (
                        "Improve your blood sugar level",
                        "Having a healthy blood sugar level is an important part of living well.",
                        "How would you like to be supported to improve your blood sugar level? (optional)",
                        "ImproveBloodSugar",
                        AllInterventions.Where(x=> x.Category == "improvebloodsugar").ToList(),
                        SelectedInterventions.ToList(),
                        CustomBarriers.FirstOrDefault(x=> x.Category == "improvebloodsugar")?.Text
                    ),
                    new ActionCategory
                    (
                        "Check your blood sugar level",
                        "Knowing your blood sugar level is important for understanding your risk of heart and circulatory diseases.",
                        "How would you like to be supported to get your blood sugar level checked? (optional)",
                        "BloodSugar",
                        AllInterventions.Where(x=> x.Category == "bloodsugar").ToList(),
                        SelectedInterventions.ToList(),
                        CustomBarriers.FirstOrDefault(x=> x.Category == "bloodsugar")?.Text
                    ),
                     new ActionCategory
                    (
                        "Check your cholesterol levels",
                        "Knowing your cholesterol levels is important for understanding your risk of heart and circulatory diseases.",
                        "How would you like to be supported to get your cholesterol levels checked? (optional)",
                        "Cholesterol",
                        AllInterventions.Where(x=> x.Category == "cholesterol").ToList(),
                        SelectedInterventions.ToList(),
                        CustomBarriers.FirstOrDefault(x=> x.Category == "cholesterol")?.Text
                    ),
                    new ActionCategory
                    (
                        "Check your blood pressure level",
                        "Knowing your blood pressure level is important for understanding your risk of heart and circulatory diseases.",
                        "How would you like to be supported to get your blood pressure level checked? (optional)",
                        "BloodPressure",
                        AllInterventions.Where(x=> x.Category == "bloodpressure").ToList(),
                        SelectedInterventions.ToList(),
                        CustomBarriers.FirstOrDefault(x=> x.Category == "bloodpressure")?.Text
                    )
                 }.Where(x => !string.IsNullOrEmpty(x.CustomBarrier) || x.HasInterventions)
                .ToList());
       
        }

        async Task<IList<Intervention>> LoadInterventionsAsync(IEnumerable<string> unsanitisedIds)
        {
            if (unsanitisedIds is null)
            {
                return null;
            }

            // There is an exclusive "none" option on the interventions checkboxes that's there for
            // if you don't want to pick any items. This is clearer for some users than simply
            // leaving the control blank.

            if (unsanitisedIds.Count() == 1 && unsanitisedIds.Single() == "none")
            {
                return new List<Intervention>();
            }

            var results = new List<Intervention>();

            var sanitisedIds = unsanitisedIds.Select(x => int.Parse(x));

            foreach (var unsanitisedId in unsanitisedIds)
            {
                if (int.TryParse(unsanitisedId, out var sanitisedId))
                {
                    results.Add(await Database.Interventions.FindAsync(sanitisedId));
                }
                else
                {
                    return null;
                }
            }

            return results;
        }

        async Task<SanitisedModel> ValidateAndSanitiseAsync(UnsanitisedModel actions)
        {
            var alcohol = await LoadInterventionsAsync(actions.Alcohol);

            var smoking = await LoadInterventionsAsync(actions.Smoking);

            var weight = await LoadInterventionsAsync(actions.Weight);

            var move = await LoadInterventionsAsync(actions.Move);

            var mental = await LoadInterventionsAsync(actions.Mental);

            var gp = await LoadInterventionsAsync(actions.GP);

            var bloodPressure = await LoadInterventionsAsync(actions.BloodPressure);

            var bloodSugar = await LoadInterventionsAsync(actions.BloodSugar);

            var cholesterol = await LoadInterventionsAsync(actions.Cholesterol);

            var improveBloodPressure = await LoadInterventionsAsync(actions.ImproveBloodPressure);

            var improveBloodSugar = await LoadInterventionsAsync(actions.ImproveBloodSugar);

            var improveCholesterol = await LoadInterventionsAsync(actions.ImproveCholesterol);

            return new SanitisedModel
            {
                Alcohol = alcohol,
                Smoking = smoking,
                Weight = weight,
                Move = move,
                GP = gp,
                Mental = mental,
                BloodPressure = bloodPressure,
                BloodSugar = bloodSugar,
                Cholesterol = cholesterol,
                ImproveBloodPressure = improveBloodPressure,
                ImproveBloodSugar = improveBloodSugar,
                ImproveCholesterol = improveCholesterol
            };
        }
    }
}