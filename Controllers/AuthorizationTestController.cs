
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DocuFlow.Api.Controllers;

[ApiController]
[Route("api/authorization-test")]
public class AuthorizationTestController : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("public")]
    public IActionResult Public()
    {
        return Ok(new
        {
            success = true,
            message = "Public endpoint dapat diakses."
        });
    }

    [Authorize]
    [HttpGet("authenticated")]
    public IActionResult Authenticated()
    {
        return Ok(new
        {
            success = true,
            message = "User berhasil terautentikasi."
        });
    }

    [Authorize(Policy = "PROJECT_VIEW")]
    [HttpGet("project-view")]
    public IActionResult ProjectView()
    {
        return Ok(new
        {
            success = true,
            message = "User memiliki permission PROJECT_VIEW."
        });
    }

    [Authorize(Policy = "USER_CREATE")]
    [HttpGet("user-create")]
    public IActionResult UserCreate()
    {
        return Ok(new
        {
            success = true,
            message = "User memiliki permission USER_CREATE."
        });
    }

    [Authorize(Policy = "DOCUMENT_APPROVE")]
    [HttpGet("document-approve")]
    public IActionResult DocumentApprove()
    {
        return Ok(new
        {
            success = true,
            message = "User memiliki permission DOCUMENT_APPROVE."
        });
    }
}
