﻿using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Bumbo.Data;
using Bumbo.Data.Models;
using Bumbo.Data.Models.Common;
using Bumbo.Data.Models.Enums;
using Bumbo.Logic.Forecast;
using Bumbo.Web.Models.Forecast;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Bumbo.Web.Controllers
{
    [Authorize(Policy = "BranchManager")]
    [Route("Branches/{branchId}/{controller}/{year?}/{week?}/{department?}")]
    [Route("Branches/{branchId}/{controller}/{year?}/{week?}")]
    public class ForecastController : Controller
    {
        private readonly RepositoryWrapper _wrapper;

        public ForecastController(RepositoryWrapper wrapper)
        {
            _wrapper = wrapper;
        }

        /// <summary>
        /// Creates the default view for viewing forecasts for 1 week
        /// </summary>
        /// <param name="branchId">Id of the branch</param>
        /// <param name="department">Filter on department</param>
        /// <param name="year">The year of the forecast</param>
        /// <param name="week">The year's week for the forecast</param>
        /// <returns>View with <see cref="ForecastViewModel"/> as parameter</returns>
        [Route("")]
        public async Task<IActionResult> Index(int branchId, Department? department, int? year, int? week)
        {
            var viewModel = new ForecastViewModel();

            // Check for default values
            if (!year.HasValue || !week.HasValue)
            {
                return RedirectToAction(nameof(Index), new
                {
                    branchId,
                    year = year ?? DateTime.Today.Year,
                    week = week ?? ISOWeek.GetWeekOfYear(DateTime.Today),
                });
            }

            // Check if date is valid
            try
            {
                ISOWeek.ToDateTime(year.Value, week.Value, DayOfWeek.Monday);
            }
            catch (Exception)
            {
                //return RedirectToAction("Index", new {branchId});
                return NotFound();
            }

            // Define viewmodel variables
            viewModel.Branch = await _wrapper.Branch.Get(b => b.Id == branchId);
            if (viewModel.Branch == null) return NotFound();

            viewModel.Department = department;

            var firstDayOfWeek = ISOWeek.ToDateTime(year.Value, week.Value, DayOfWeek.Monday);

            viewModel.Forecasts = await _wrapper.Forecast.GetAll(
                f => f.BranchId == branchId,
                f => f.Department == department || department == null,
                // Week filter
                f => f.Date >= firstDayOfWeek,
                f => f.Date < firstDayOfWeek.AddDays(7)
            );

            viewModel.Week = week.Value;
            viewModel.Year = year.Value;
            viewModel.EditForecast = new ForecastViewModel.EditForecastViewModel();

            return View(viewModel);
        }

        // GET: Forecast/Create
        [Route("create")]
        public async Task<IActionResult> Create(int branchId, int year, int week)
        {
            var data = new StockclerkViewModel()
            {
                FirstDayOfWeek = ISOWeek.ToDateTime(year, week, DayOfWeek.Monday),
                BranchId = branchId,
                DaysInForecast = 7
            };

            return View(data);
        }

        // POST: Forecast/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("create")]
        public async Task<IActionResult> Create(int branchId, int year, int week, [FromForm] StockclerkViewModel stockclerkViewModel)
        {
            if (!ModelState.IsValid) return RedirectToAction();

            var forecastStandards =
                _wrapper.ForecastStandard.GetAll(
                    f => f.BranchForecastStandards.All(bf => bf.BranchId != branchId)
                ).Result.ToList<IForecastStandard>();

            forecastStandards.AddRange(await _wrapper.BranchForecastStandard.GetAll(
                bf => bf.BranchId == branchId
            ));


            var forecastLogic = new ForecastLogic(forecastStandards);

            for (var i = 0; i < stockclerkViewModel.ExpectedNumberOfColi.Count; i++)
            {
                var forecast = new Forecast();
                forecast.BranchId = branchId;
                forecast.Date = ISOWeek.ToDateTime(year, week, DayOfWeek.Monday).AddDays(i);

                forecast.Department = Department.KAS;
                forecast.WorkingHours = forecastLogic.GetWorkHoursCashRegister(forecast.Date);
                await _wrapper.Forecast.Add(forecast);

                forecast.Department = Department.VER;
                forecast.WorkingHours = forecastLogic.GetWorkHoursFresh(forecast.Date);
                await _wrapper.Forecast.Add(forecast);

                forecast.Department = Department.VAK;
                forecast.WorkingHours = forecastLogic.GetWorkHoursStockClerk(stockclerkViewModel.MetersOfShelves[i], stockclerkViewModel.ExpectedNumberOfColi[i]);
                await _wrapper.Forecast.Add(forecast);
            }

            return RedirectToAction("Index",
            new
            {
                branchId,
                year,
                week
            });
        }

        [Route("edit")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public virtual async Task<IActionResult> Edit(int branchId, int year, int week, [FromForm] DateTime date, Department department, int hours, int minutes)
        {
            var workingHours = hours + (decimal)minutes / 60;
            var model = new Forecast
            {
                BranchId = branchId,
                Department = department,
                Date = date,
                WorkingHours = workingHours
            };

            try
            {
                await _wrapper.Forecast.Update(model);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (await _wrapper.Forecast.Get(f => f.Equals(model)) == null) return NotFound();

                throw;
            }

            return RedirectToAction(nameof(Index),
            new
            {
                branchId,
                year,
                week
            });
        }
    }
}
