using ApiServer.DB;
using Microsoft.AspNetCore.Mvc;

namespace ApiServer.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _context;
    
    public AdminController(AppDbContext context)
    {
        _context = context;
    }
    
    
}