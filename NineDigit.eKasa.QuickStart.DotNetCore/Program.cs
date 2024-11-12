using Microsoft.Extensions.Logging;
using NineDigit.eKasa.Configuration.DataSources;
using NineDigit.eKasa.Validation;
using Serilog;
using Serilog.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NineDigit.eKasa.QuickStart;

class Program
{
    /// <summary>
    /// Application entry point.
    /// </summary>
    static void Main()
    {
        // Before running this app, please make sure that you have:
        // 1, connected CHDU device to this computer
        // 2, setup all settings using Portos eKasa servis application.

        try
        {
            using QuickStartApp app = new();
            
            Task task = app.Run(); // run the demo

            task.Wait(); // wait for asynchronous task to complete.

            // end of using block will call `IDisposable` interface automatically.
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }

        Console.WriteLine("Press enter to exit.");
        Console.ReadLine();
    }
}

public class QuickStartApp : IDisposable
{
    readonly Client client;

    /// <summary>
    /// This is the cash register code from example XML files.
    /// </summary>
    readonly ORPCode cashRegisterCode = "88812345678900001";

    public QuickStartApp()
    {
        // 1, Configuration loading

        // With Portos eKasa, windows application called "Portos eKasa servis" is shipped.
        // With portos ekasa servis, you can change all necessary settings.
        // The settings are saved in JSON file.
        // The goal is to use exactly same JSON configuration file that is edited by servis application.
        // Please make sure that you have installed servis application and configured serial port and other necessary things.

        // Lets load configuration from JSON file.
        ClientConfiguration configuration = this.LoadClientConfiguration();

        // 2, Logging

        // Client class accepts ILoggerFactory instance.
        // Thanks to this abstract approach, you can use your favorite logging library.

        // In this example, we are using Serilong, with following nuget packages:
        // - Serilog.Extensions.Logging
        // - Serilog.Sinks.Console

        Serilog.ILogger serilogLogger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .CreateLogger();

        // create universal Microsoft.Extensions.Logging.ILoggerFactory instance
        ILoggerFactory loggerFactory = new SerilogLoggerFactory(serilogLogger);

        // we can test the logger:
        loggerFactory.CreateLogger(nameof(QuickStartApp)).LogDebug("Hello from QuickStartApp!");

        // 3, Client instantiation

        // Now, lets instantiate the entry point to whole solution - instance of Client class.

        this.client = new Client(configuration, loggerFactory);

        // The instantiation of library is now completed.
        // Your application should keep only one instance of client.
    }

    /// <summary>
    /// Loads JSON configuration file from default location.
    /// </summary>
    /// <returns></returns>
    private ClientConfiguration LoadClientConfiguration()
    {
        // this is the working directory path, usually located at: "C:/ProgramData/NineDigit/Portos.eKasa"
        string ekasaWorkingDirectoryPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "NineDigit",
            "Portos.eKasa");

        string configurationFilePath = Path.Combine(ekasaWorkingDirectoryPath, "settings.json");

        // to read config from JSON files, instantiate `JsonClientConfigurationFileDataSource`
        IDataSource<ClientConfiguration> configurationDataSource = new JsonClientConfigurationFileDataSource(configurationFilePath);

        // load the configuration for eKasa client
        ClientConfiguration configuration = configurationDataSource.Load();

        return configuration;
    }

    public async Task Run()
    {
        // if cash drawer is connected to the printer, the drawer will open now.
        await this.client.OpenDrawerAsync(CancellationToken.None);

        // lets print some nonfiscal text with custom text formatting.
        await this.PrintNonfiscalText();

        // now lets print receipt (with no options specified).
        CashRegisterReceipt receipt = this.CreateReceipt();
        await this.PrintPaperReceipt(receipt);

        // previous call was made with default options - paper receipt.
        // if we want to specify, how receipt should be issued (as paper, as PDF file or via email) we can compose printing options.
        // lets see examples below:

        // paper receipt
        CashRegisterReceipt anotherPaperReceipt = this.CreateReceipt();
        await this.PrintPaperReceiptWithCustomOptions(anotherPaperReceipt);

        // PDF receipt
        CashRegisterReceipt pdfReceipt = this.CreateReceipt();
        await this.PrintPdfReceipt(pdfReceipt);

        // email receipt
        CashRegisterReceipt emailReceipt = this.CreateReceipt();
        await this.PrintEmailReceipt(emailReceipt);

        // location registration
        await this.LocationRegistration();
    }

    private Task PrintNonfiscalText()
    {
        // instantiate string builder, to append string more efficiently.
        StringBuilder sb = new StringBuilder();

        // plain text
        sb.AppendLine("Hello world!");

        // text with formatting
        sb.Append(TextToken.Create("This line is bold.", TextFormats.Bold, TextAlignment.Center).ToString());

        sb.AppendLine("After text token, newline is created automatically.");

        sb.Append(TextToken.Create("This line is underlined.", TextFormats.Underlined).ToString());

        // "You can combine TextFormat values by using | operator
        sb.Append(TextToken.Create("This line is bold and underlined.", TextFormats.Underlined | TextFormats.Bold).ToString());

        sb.AppendLine(TextToken.Create("Hello world!", TextFormats.DoubleHeight | TextFormats.DoubleWidth | TextFormats.Underlined | TextFormats.Inverted | TextFormats.Bold).ToString());

        // Paper cut follows now.
        sb.Append(new PageBreakToken().ToString());

        // We can print either barcodes or QR codes easily!
        sb.AppendLine(BarcodeToken.Create("1234567", BarcodeType.EAN8, BarcodeHriPosition.Above).ToString());
        sb.AppendLine(BarcodeToken.Create("123456789012", BarcodeType.EAN13, BarcodeHriPosition.Below).ToString());
        sb.AppendLine(BarcodeToken.Create("1234ABCD39", BarcodeType.Code39, BarcodeHriPosition.Both).ToString());
        sb.AppendLine(BarcodeToken.Create("1234ABCD93", BarcodeType.Code93, height: 30).ToString());
        sb.AppendLine(BarcodeToken.Create("1234567890", BarcodeType.Code128, elementWidth: 2).ToString());

        sb.AppendLine(QrCodeToken.Create("https://www.ninedigit.sk").ToString());

        string text = sb.ToString();

        if (!ReceiptText.IsValid(text))
        {
            throw new InvalidOperationException("We have used some forbidden characters in our text output!");
        }

        ReceiptText receiptText = new ReceiptText(text);

        TextPrintContext context = new TextPrintContext(receiptText);

        return this.client.PrintTextAsync(context, CancellationToken.None);
    }

    private CashRegisterReceipt CreateReceipt()
    {
        // lets create receipt object - the single required parameter is the cash registe code.
        // all other parameters are optional.

        CashRegisterReceipt receipt = new CashRegisterReceipt(this.cashRegisterCode)
        {
            // Optional text, printed between informations about company and items
            HeaderText = "Web: ekasa.ninedigit.sk",
            // Optional text, that is printed at the end of the receipt
            FooterText = "Ďakujeme za nákup.",
        };

        // lets add some items (products) to the receipt - as receipt must have at least one.

        // helper object to contains receipt item data.
        ReceiptItemData itemData = new ReceiptItemData()
        {
            Type = ReceiptItemType.Positive,
            Name = "Banány voľné",
            UnitPrice = 1.123456m, // unit price can be specified up to 6 decimal places
            Quantity = new Quantity(0.123m, "kg"), // quantity can be specified up to 3 decimal places
            Price = 0.14m, // price must be equal to unitPrice * quantity, and can be specified up to 2 decimal places. Mathematical rounding is applied.
            VatRate = VatRate.Zero
        };

        // this data object has its validator, so we can check, whether our application composes receipt item correctly.
        ValidationResult itemDataValidationResult = itemData.Validate();
        if (!itemDataValidationResult.IsValid)
        {
            // object is invalid - that means, we did something wrong!

            // to see more details, inspect the errors collection.
            IEnumerable<MemberValidationFailure> errors = itemDataValidationResult.Errors;

            // lets pick first error and throw an exception.
            MemberValidationFailure firstError = errors.First();

            string errorMessage = $"Invalid composition of ticket item. {firstError.MemberName}: {firstError.Message}";
            throw new InvalidOperationException(errorMessage);
        }

        // after validation succeeds, we can create receipt item
        ReceiptItem receiptItem = new ReceiptItem(itemData);
        // add item to receipt.
        receipt.Items.Add(receiptItem);

        // we don't need to specify payments. But if we do, their amounts must be equal or greater than total amount of receipt.

        // prepare empty payments collection
        receipt.Payments = new ReceiptPayments();

        // and add some payments
        ReceiptPayment payment1 = new ReceiptPayment("Hotovosť", 1m); // "Hotovosť" means "cash"
        receipt.Payments.Add(payment1);

        // cash to return can be stated as another payment, with negative amount.
        ReceiptPayment payment2 = new ReceiptPayment("Hotovosť", -0.86m);
        receipt.Payments.Add(payment2);

        return receipt;
    }

    private async Task PrintPaperReceipt(CashRegisterReceipt receipt)
    {
        // prepare request object
        RegisterCashRegisterReceiptRequest receiptRequest = new RegisterCashRegisterReceiptRequest(receipt);

        // and register receipt
        RegisterReceiptResult result = await client.RegisterReceiptAsync(receiptRequest, CancellationToken.None);
    }

    private async Task PrintPaperReceiptWithCustomOptions(CashRegisterReceipt receipt)
    {
        // we can override default client configuration with these options:
        PosPrintingOptions printOptions = new PosPrintingOptions()
        {
            // this will override the "configuration.Printers.Pos.Drawer.Enabled" for this specific receipt.
            OpenDrawer = false,
            // this will override the "configuration.Printers.Pos.Logo.Enabled" for this specific receipt.
            PrintLogo = true,
            // this will override the "configuration.Printers.Pos.Logo.MemoryAddress" for this specific receipt.
            LogoMemoryAddress = 2
        };

        // prepare print context object from printer name and printing options
        RegisterReceiptPrintContext printContext = RegisterReceiptPrintContext.CreatePos(printOptions);

        // wrap receipt to request
        RegisterCashRegisterReceiptRequest receiptRequest = new RegisterCashRegisterReceiptRequest(receipt);

        // and register receipt
        RegisterReceiptResult result = await client.RegisterReceiptAsync(receiptRequest, printContext, CancellationToken.None);

        this.HandleReceiptRegistrationResult(result);
    }

    private async Task PrintPdfReceipt(CashRegisterReceipt receipt)
    {
        // there are no options available for pdf printer.

        // prepare print context object without additional options.
        RegisterReceiptPrintContext printContext = RegisterReceiptPrintContext.CreatePdf();

        // wrap receipt to request
        RegisterCashRegisterReceiptRequest receiptRequest = new RegisterCashRegisterReceiptRequest(receipt);

        // and register receipt
        RegisterReceiptResult result = await client.RegisterReceiptAsync(receiptRequest, printContext, CancellationToken.None);

        // PDF file is created now.
        // by default, its location is : "C:/ProgramData/NineDigit/Portos.eKasa/receipts"

        this.HandleReceiptRegistrationResult(result);
    }

    private async Task PrintEmailReceipt(CashRegisterReceipt receipt)
    {
        // create printing options object from dictionary.
        EmailPrintingOptions printOptions = new EmailPrintingOptions()
        {
            // required parameter - recipients email address
            // please change this to some real email address
            To = "john.brown@example.com",
            // optional recipient display name
            RecipientDisplayName = "John Brown",
            // optional parameter. this will override the "configuration.Printers.Email.Subject" for this specific receipt.
            Subject = "Your receipt, mr. Brown!",
            // optional parameter. This will override the "configuration.Printers.Email.Body" for this specific receipt.
            Body = "Thank you for your purchase."
        };

        // prepare print context object from printing options
        RegisterReceiptPrintContext printContext = RegisterReceiptPrintContext.CreateEmail(printOptions);

        // wrap receipt to request
        RegisterCashRegisterReceiptRequest receiptRequest = new RegisterCashRegisterReceiptRequest(receipt);

        // and register receipt
        RegisterReceiptResult result;

        try
        {
            result = await client.RegisterReceiptAsync(receiptRequest, printContext, CancellationToken.None);
        }
        catch (Exception ex)
        {
            throw new Exception("please setup your email configuration (SMTP server, ...) before sending emails.", ex);
        }

        this.HandleReceiptRegistrationResult(result);
    }

    private void HandleReceiptRegistrationResult(RegisterReceiptResult result)
    {
        if (result.IsSuccessful == true)
        {
            string message = $"Receipt was registered in ONLINE mode, with unique ID: " + result.Response.Data.Id;
            Console.WriteLine(message);
        }
        else if (result.IsSuccessful == null)
        {
            string message = $"Receipt was registered in OFFLINE mode, no ID is available. As replacement for ID, OKP is used: {result.Request.Data.OKP}";
            Console.WriteLine(message);
        }
        else if (result.IsSuccessful == false)
        {
            string message = $"eKasa system rejected our request for receipt registration. Error #{result.Error.Code}: {result.Error.Message}";
            Console.WriteLine(message);
        }
    }

    private async Task LocationRegistration()
    {
        // all portable cash registers are required to register their location, after their location change.

        // location can be specified in three forms:

        // 1, GPS coordinates
        GeoCoordinates locationGps = new GeoCoordinates(
            longitude: 17.165377m,
            latitude: 48.148962m);

        // 2, Address
        PhysicalAddress addressLocation = new PhysicalAddress(
            streetName: "Kresánkova",
            municipality: "Bratislava",
            buildingNumber: "12",
            postalCode: "84105",
            propertyRegistrationNumber: 3597);

        // 3, Free form, such as car licence plate.
        CashRegisterOtherLocation locationOther = new CashRegisterOtherLocation(
            "Taxi, ŠPZ: BL-123AA");

        // wrap location to cash register location object, to pair this location with cash register code.
        CashRegisterLocation cashRegisterLocation = new CashRegisterLocation(cashRegisterCode, addressLocation);

        // wrap cashRegisterLocation to request object.
        RegisterLocationRequest request = new RegisterLocationRequest(cashRegisterLocation);

        // finally, send location registration to eKasa server.
        RegisterLocationResult registrationResult = await client
            .RegisterLocationAsync(request, CancellationToken.None)
            .ConfigureAwait(false);

        // registration result analysis is similiar to receipt. There are three scenarios:

        if (registrationResult.IsSuccessful == true)
        {
            // 1, location has been registered in ONLINE mode and has been successful.
        }
        else if (registrationResult.IsSuccessful == null)
        {
            // 2, location has been registered in OFFLINE mode.
            // the library will try to send this offline message later, when connection with ekasa server will be established.
        }
        else if (registrationResult.IsSuccessful == false)
        {
            // 3, location registration has failed. Please see error for more details.
            EKasaError error = registrationResult.Error;
        }
    }

    public void Dispose()
    {
        this.client.Dispose();
    }
}
