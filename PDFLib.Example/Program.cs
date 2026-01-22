using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Mvc;
using PDFLib.Chromium;
using PDFLib.Chromium.Hosting;
using PDFLib.Example;
using PDFLib.Example.Components;
using PDFLib.Example.Components.Pages;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddScoped(_ => new HttpClient // So we can download things from our components
    {
        BaseAddress = new Uri(builder.Configuration["HttpClient:BaseAddress"] ?? "http://localhost:5130")
    })
    .AddScoped<BlazorRenderer>() // Example service to convert blazor into HTML so we can print it
    .AddPdfService() // The Pdf Service we're trying to test/showcase
    .AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.MapGet("/print", async (HttpContext context,[FromServices] PdfService pdfService, [FromServices] BlazorRenderer renderer) =>
{
    // We can write directly to the response body
    var html = await renderer.RenderComponent<Report>();
    await pdfService.RenderPdfAsync(html, context.Response.Body);
});

app.MapGet("/signature", async ([FromServices] PdfService pdfService, [FromServices] BlazorRenderer renderer) =>
{
    var html = await renderer.RenderComponent<Report>();
    var ms = new MemoryStream();

    // Obviously, this is for example-sake. You wouldn't handle the certs in this way 
    using var rsa1 = RSA.Create(2048);
    var request1 = new CertificateRequest("cn=Test Signer 1", rsa1, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    using var cert1 = request1.CreateSelfSigned(DateTimeOffset.Now, DateTimeOffset.Now.AddYears(1));
    
    await pdfService.RenderSignedPdfAsync(html, ms, new()
    {
        {"signature-area-1", new Signature("Test Signer 1", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))}
    },
        signer =>
    {
        // Because the signature-area-1 on our Report page.
        signer.AddCertificate(cert1, "signature-area-1");
    });

    /*
     * Since we were writing to the stream above, we have to remember
     * to reset the pointer to the beginning; otherwise, you'll get a
     * content length mismatch because the stream position is at the end
     */
    
    ms.Seek(0, SeekOrigin.Begin);
    return Results.File(
        fileStream: ms,
        contentType: "application/pdf",
        fileDownloadName: "SignedReport.pdf"
    );
});

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();