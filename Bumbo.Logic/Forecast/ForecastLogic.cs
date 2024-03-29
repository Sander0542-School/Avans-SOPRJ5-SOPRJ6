﻿using System;
using System.Collections.Generic;
using System.Linq;
using Bumbo.Data.Models.Common;
using Bumbo.Data.Models.Enums;
namespace Bumbo.Logic.Forecast
{
    public class ForecastLogic
    {
        private static readonly int RoundingFactor = 2;
        
        private readonly decimal _customersPerHourCashRegister;
        private readonly decimal _customersPerHourProduceDepartment;
        private readonly decimal _minutesPerColiStockShelves;
        private readonly decimal _minutesPerColiUnloading;
        private readonly decimal _secondsPerMeterFacing;
        private readonly Dictionary<DayOfWeek, int> _expectedNumberOfCustomers;

        /// <summary>
        ///     The number of decimals to which returned values will be rounded.
        /// </summary>
        public ForecastLogic(List<IForecastStandard> forecastStandards, Dictionary<DayOfWeek, int> expectedNumberOfCustomers)
        {
            _expectedNumberOfCustomers = expectedNumberOfCustomers;
            foreach (var f in forecastStandards)
            {
                switch (f.Activity)
                {
                    case ForecastActivity.UNLOAD_COLI:
                        _minutesPerColiUnloading = f.Value;
                        break;
                    case ForecastActivity.STOCK_SHELVES:
                        _minutesPerColiStockShelves = f.Value;
                        break;
                    case ForecastActivity.CASHIER:
                        _customersPerHourCashRegister = f.Value;
                        break;
                    case ForecastActivity.PRODUCE_DEPARTMENT:
                        _customersPerHourProduceDepartment = f.Value;
                        break;
                    case ForecastActivity.FACE_SHELVES:
                        _secondsPerMeterFacing = f.Value;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(forecastStandards),
                        "ForecastLogic encountered an unknown enum value. Did you add a new enum to ForecastActivity?");
                }
            }
        }

        public decimal GetWorkHoursCashRegister(DateTime date)
        {
            return Math.Round(WorkHoursCashRegister(NumberOfCustomersExpected(date)), RoundingFactor);
        }

        public decimal GetWorkHoursFresh(DateTime date)
        {
            return Math.Round(WorkHoursFresh(NumberOfCustomersExpected(date)), RoundingFactor);
        }

        public decimal GetWorkHoursStockClerk(decimal metersOfShelves, decimal expectedNumberOfColi)
        {
            return Math.Round(WorkHoursUnloading(expectedNumberOfColi) + WorkHoursFacingShelves(metersOfShelves) +
                              WorkHoursStockingColi(expectedNumberOfColi), RoundingFactor);
        }


        /// <summary>
        ///     Calculates the number of hours which is required for the amount of coli that is expected.
        /// </summary>
        /// <param name="expectedNumberOfColi">Number of coli that is expected</param>
        /// <returns>Number of hours required to unload all the coli</returns>
        public decimal WorkHoursUnloading(decimal expectedNumberOfColi)
        {
            return Math.Round(expectedNumberOfColi * _minutesPerColiUnloading / 60, RoundingFactor);
        }

        /// <summary>
        ///     Calculates the amount of working hours for the cash registers for a specific date
        /// </summary>
        /// <param name="numberOfCustomersExpected">Number of expected customers for a given day</param>
        /// <returns> returns a 2 decimal number representing the working hours for the cash registers for a specific date</returns>
        public decimal WorkHoursCashRegister(decimal numberOfCustomersExpected)
        {
            return numberOfCustomersExpected / _customersPerHourCashRegister;
        }

        /// <summary>
        ///     Calculates the amount of working hours for the fresh for a specific date
        /// </summary>
        /// <param name="numberOfCustomersExpected">Number of expected customers for a given day</param>
        /// <returns> returns a 2 decimal number representing the working hours for the fresh for a specific date</returns>
        public decimal WorkHoursFresh(decimal numberOfCustomersExpected)
        {
            return Math.Round(numberOfCustomersExpected / _customersPerHourProduceDepartment, RoundingFactor);
        }


        public int NumberOfCustomersExpected(DateTime date)
        {
            return _expectedNumberOfCustomers.FirstOrDefault(kvp => date.DayOfWeek == kvp.Key).Value;

            /* Disabled in favour of acceptatie test scenario
            // begin met een standaard hoeveelheid
            var _numberOfCustomersExpected = 1785;

            // Haal mensen eraf of voeg mensen toe op basis van de dag van de week
            _numberOfCustomersExpected = date.DayOfWeek switch
            {
                DayOfWeek.Monday => (int)(_numberOfCustomersExpected * 0.90),
                DayOfWeek.Tuesday => (int)(_numberOfCustomersExpected * 0.95),
                DayOfWeek.Wednesday => (int)(_numberOfCustomersExpected * 1.0),
                DayOfWeek.Thursday => (int)(_numberOfCustomersExpected * 1.05),
                DayOfWeek.Friday => (int)(_numberOfCustomersExpected * 1.10),
                DayOfWeek.Saturday => (int)(_numberOfCustomersExpected * 1.0),
                _ => (int)(_numberOfCustomersExpected * 0.90)// It sure would be nice if Sunday always was the default value
            };

            // TODO: haal mensen eraf of voeg mensen toe op basis van het weer
            // haal mensen eraf of voeg mensen toe op basis van feestdagen
            // if (holidays.FirstOrDefault(predicate: e => e.Date.Subtract(new TimeSpan(1, 0, 0, 0)) == date) != null)
            // {
            //     _numberOfCustomersExpected = (int) (_numberOfCustomersExpected * 1.20);
            // }

            return _numberOfCustomersExpected;
            */
        }

        /// <summary>
        ///     Calculates the total number of hours that need to be worked depending on the amount of coli that's expected
        /// </summary>
        /// <param name="expectedNumberOfColi">Number of coli which needs to be stocked in shelves</param>
        /// <returns>Total number of hours required to work to stock all the coli. Specific to two decimals</returns>
        public decimal WorkHoursStockingColi(decimal expectedNumberOfColi)
        {
            return Math.Round(expectedNumberOfColi * _minutesPerColiStockShelves / 60, RoundingFactor);
        }

        /// <summary>
        ///     Calculates the number of hours which is spent facing a given distance of shelves
        /// </summary>
        /// <param name="metersOfShelves">The amount of shelves in meters which need to be faced</param>
        /// <returns>Number of hours required to face shelves</returns>
        public decimal WorkHoursFacingShelves(decimal metersOfShelves)
        {
            return Math.Round(_secondsPerMeterFacing * metersOfShelves / 60, RoundingFactor);
        }
    }
}
