using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

public abstract class BaseApiController : ControllerBase
{
    protected int? GetBusinessId()
    {
        // Prioritize "businessId" claim, then fallback to ClaimTypes.GroupSid
        var claim = User.FindFirstValue("businessId") ?? User.FindFirstValue(ClaimTypes.GroupSid);
        return int.TryParse(claim, out var id) ? id : null;
    }

    protected int? GetEmployeeId()
    {
        // Employee JWT carries "employeeId" claim (set by EmployeeAuthController)
        var claim = User.FindFirstValue("employeeId") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(claim, out var id) ? id : null;
    }
}