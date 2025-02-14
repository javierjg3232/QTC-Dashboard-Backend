using AuthECAPI.Models;
using AuthECAPI.DTO;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Formats.Asn1;
using CsvHelper;
using CsvHelper.Configuration;
using System;
using System.Globalization;

namespace AuthECAPI.Controllers
{
    [Route("api/survey")]
    [ApiController]
    public class SurveyController : ControllerBase
    {
        private readonly AppDbContext _context;

        public SurveyController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadSurvey([FromForm] string category, [FromForm] DateTime surveyDate, [FromForm] IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("Please upload a CSV file.");
            }

            try
            {
                // Save file to a directory (Optional: adjust path as needed)
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "Uploads");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                var filePath = Path.Combine(uploadsFolder, file.FileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // Save metadata in the database
                var surveyResponse = new SurveyResponse
                {
                    Category = category,
                    SurveyDate = surveyDate,
                    FileName = file.FileName,
                    UploadedBy = "Admin" // Change to actual user if authentication is implemented
                };

                _context.SurveyResponses.Add(surveyResponse);
                await _context.SaveChangesAsync();

                return Ok(new { message = "File uploaded successfully!", surveyId = surveyResponse.Id });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpGet("survey-types")]
        public async Task<IActionResult> GetSurveyTypes()
        {
            // Assuming SurveyResponse table has a column 'SurveyType'
            var surveyTypes = await _context.SurveyResponses
                .Select(sr => sr.Category) // Get distinct survey types if needed
                .Distinct()
                .ToListAsync();

            // Trim whitespace for single-word entries
            var processedSurveyTypes = surveyTypes
                .Select(type => type.Contains(" ") ? type : type.Trim())
                .ToList();

            return Ok(processedSurveyTypes);
        }

        [HttpPost("filter")]
        public async Task<IActionResult> GetFilteredSurveyData([FromBody] SurveyFilterDto filterDto)
        {
            // Check if the 'Uploads' folder exists
            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "Uploads");

            if (!Directory.Exists(uploadsFolder))
            {
                return NotFound("Uploads directory not found.");
            }

            // Start building the query on SurveyResponses table
            var query = _context.SurveyResponses.AsQueryable();

            // Apply Category filter if SurveyType is provided
            if (!string.IsNullOrEmpty(filterDto.SurveyType))
            {
                query = query.Where(s => EF.Functions.Like(s.Category, $"%{filterDto.SurveyType}%"));
            }

            // Apply SurveyDate filter
            DateTime startDate = filterDto.DateRange switch
            {
                "Last 7 days" => DateTime.UtcNow.AddDays(-7),
                "Last 30 days" => DateTime.UtcNow.AddDays(-30),
                "This year" => new DateTime(DateTime.UtcNow.Year, 1, 1),
                _ => DateTime.MinValue
            };

            if (startDate != DateTime.MinValue)
            {
                query = query.Where(s => s.SurveyDate >= startDate);
            }

            // Get the filtered SurveyResponses from the database
            var filteredSurveys = await query.ToListAsync();

            // List to hold the final survey data from CSV files
            var surveyData = new List<Dictionary<string, string>>();

            // Iterate through the filtered surveys and read the data from the corresponding CSV files
            foreach (var survey in filteredSurveys)
            {
                // Get the full file path using FileName from the SurveyResponse record
                var filePath = Path.Combine(uploadsFolder, survey.FileName);

                // Check if the file exists in the 'Uploads' folder
                if (System.IO.File.Exists(filePath))
                {
                    // Read the CSV file content and add it to the surveyData list
                    using (var reader = new StreamReader(filePath))
                    using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                    {
                        var records = csv.GetRecords<dynamic>().ToList();

                        // Add each record's data to the surveyData list as a dictionary
                        foreach (var record in records)
                        {
                            var recordDict = ((IDictionary<string, object>)record)
                                .ToDictionary(k => k.Key, k => k.Value?.ToString() ?? "");
                            surveyData.Add(recordDict);
                        }
                    }
                }
            }

            // Return the collected survey data from the CSV files
            return Ok(surveyData);
        }

        [HttpGet("all-uploads")]
        public async Task<IActionResult> GetAllSurveyResponses()
        {
            var surveys = await _context.SurveyResponses.ToListAsync();
            return Ok(surveys);
        }

        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> DeleteFile(int id)
        {
            var response = await _context.SurveyResponses.FindAsync(id);
            if (response == null)
            {
                return NotFound(new { message = "File not found." });
            }

            // Define the file path based on where you store uploaded files
            var filePath = Path.Combine("Uploads", response.FileName); // Adjust path as needed

            // Check if the file exists and delete it
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }

            // Remove the database record
            _context.SurveyResponses.Remove(response);
            await _context.SaveChangesAsync();

            return Ok(new { message = "File deleted successfully." });
        }



    }
}
