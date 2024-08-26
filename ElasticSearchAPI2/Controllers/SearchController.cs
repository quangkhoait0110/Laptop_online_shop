// Controllers/SearchController.cs
using ElasticSearchAPI2.Services;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace ElasticSearchAPI2.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class SearchController : ControllerBase
    {
        private readonly SearchService _searchService;

        public SearchController(SearchService searchService)
        {
            _searchService = searchService;
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetDocumentById(string id)
        {
            var product = await _searchService.GetDocumentByIdAsync(id);
            if (product == null)
            {
                return NotFound(new { Error = "Document not found" });
            }
            return Ok(product);
        }

        [HttpGet("search")]
        public async Task<IActionResult> Search(string query)
        {
            var results = await _searchService.SearchAsync(query);
            if (results == null)
            {
                return StatusCode(500, new { Error = "An error occurred during the search" });
            }
            return Ok(results);
        }
    }
}