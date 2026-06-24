# PDF Example App

This is a very barebones example of how to leverage the PDF library
within an ASP.NET Core application.

The HtmlRenderer that was introduced a few dotnet versions ago has a limitation which requires
a workaround. It only renders the component itself, does not include the default layout nor include the `@layout` directive.

In order to render the component with the expected CSS and Javascript you must use a [Wrapper component](./PDFLib.Example/Components/Pages/Wrapper.razor).

This wrapper is mostly a copy of the `App.razor` with a few modifications.

-------

## Examples

### Weather Report

This example showcases how to use the PDF library to render a component with the expected CSS and Javascript. 

... Yes you can totally just use the browsers print functionality but that's not the point. This proves that you server
side render a component into a PDF document.

### Print Page (without signature)

The report page without the signature loaded.

### Get Report (with signature)

The report page with the signature loaded into the signature area.
