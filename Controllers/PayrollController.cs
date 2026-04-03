using backend.DTOs.Payroll;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class PayrollController(IPayrollService payrollService) : ControllerBase
{
    [HttpPost("calculate")]
    public ActionResult<PayrollResponse> Calculate(PayrollRequest request)
    {
        if (request.GrossMonthlySalary <= 0)
        {
            return BadRequest("Gross monthly salary must be greater than zero.");
        }

        return Ok(payrollService.Calculate(request.GrossMonthlySalary));
    }
}
