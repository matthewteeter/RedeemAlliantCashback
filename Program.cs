﻿using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Playwright;
using System;
using System.Runtime.ConstrainedExecution;
using System.Threading.Tasks;

using var playwright = await Playwright.CreateAsync();
var b = playwright.Firefox;
if (args.Any() && args?[0] == "install")
{
    Environment.Exit(Microsoft.Playwright.Program.Main(new[] { "install", b.Name }));
}
bool inDocker = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true";
Console.WriteLine(!inDocker ? "Starting Alliant Cashback Redemption..." : "Starting Alliant Cashback Redemption in headless mode...");
using IHost host = Host.CreateDefaultBuilder(args)
                       .UseEnvironment("Development") //enable user secrets in Development for local overrides
                       .Build();
                       //.ConfigureAppConfiguration(config => config.AddHcpVaultSecretsConfiguration(config.Build())).Build();
// if running locally, you can set the parameters using dotnet user-secrets. If docker, pass in via Env Vars.
IConfiguration config = host.Services.GetRequiredService<IConfiguration>();
await using var browser = await b.LaunchAsync(new BrowserTypeLaunchOptions { Headless = inDocker });
var context = await browser.NewContextAsync();
var page = await context.NewPageAsync();

Console.WriteLine("Going to homepage...");
await page.GotoAsync("https://www.alliantcreditunion.org/");
Console.WriteLine("Clicking Login...");
await page.GetByRole(AriaRole.Link, new() { Name = "Secure Log In " }).ClickAsync();
Console.WriteLine("Logging in....");
await page.Locator("#ctl00_pagePlaceholder_txt_username_new").FillAsync(config["AlliantUsername"]);
await page.Locator("#ctl00_pagePlaceholder_txt_password_new").FillAsync(config["Password"]);
await page.Locator("#ctl00_pagePlaceholder_txt_password_new").PressAsync("Enter");

await DetectSecurityQuestionAndSetAnswer(config);
await page.GetByRole(AriaRole.Button, new() { Name = "Continue" }).ClickAsync();
await page.GetByRole(AriaRole.Link, new() { Name = "No thanks. I want to keep" }).ClickAsync();
Console.WriteLine("Clicking Cashback Visa Signature and Manage Account...");
await page.GetByRole(AriaRole.Link, new() { Name = "Cashback Visa Signature C..." }).ClickAsync();
await page.GetByRole(AriaRole.Button, new() { Name = "Manage Account" }).ClickAsync();
//Clicking Alliant Cashback opens a new tab, which Playwright tracks using a new Page
var newPage = await context.RunAndWaitForPageAsync(async () =>
{
    await page.GetByRole(AriaRole.Link, new() { Name = "Alliant Cashback" }).ClickAsync();
});
await Task.Delay(5000); //helps avoid CSRF error
Console.WriteLine($"Tabs: {browser.Contexts[0].Pages.Count}");
await newPage.GetByRole(AriaRole.Button, new() { Name = "Next" }).ClickAsync();
Console.WriteLine("Clicked Next...");

await newPage.GetByLabel("Confirm email address*").FillAsync(config["Email"]);
await newPage.GetByRole(AriaRole.Button, new() { Name = "Continue" }).ClickAsync();
Console.WriteLine("Clicked Continue...");

await newPage.GetByRole(AriaRole.Button, new() { Name = "order" }).ClickAsync();//place my order
Console.WriteLine("Cashback redeemed!");

async Task DetectSecurityQuestionAndSetAnswer(IConfiguration config)
{
    Console.WriteLine("Reading security question...");
    string answer = string.Empty;
    string selectorToSupplyAnswer = "ddlAnswer";//most answers are input via a dropdown list, but for some, free-form text goes into a textbox
    string question = await page.Locator($"#ctl00_pagePlaceholder_SecurityQuestionUserControl_lblQuestion").InnerTextAsync();
    if (question.Contains("What is your favorite marine"))
    {
        answer = config["SecurityQuestions:FavoriteMarineAnimal"];
    }
    else if (question.Contains("What is the make of your favorite car"))
    {
        answer = config["SecurityQuestions:MakeOfFavoriteCar"];
    }
    else if (question.Contains("favorite candy"))
    {
        answer = config["SecurityQuestions:FavoriteCandy"];
    }
    else if (question.Contains("What is the first letter of your grandfather"))
    {
        answer = config["SecurityQuestions:FirstLetterGrandfather"];
    }
    else if (question.Contains("favorite food"))
    {
        answer = config["SecurityQuestions:FavoriteFood"];
        selectorToSupplyAnswer = "txtAnswer";
    }
    else if (question.Contains("day of the month was your mother born"))
    {
        answer = config["SecurityQuestions:DayOfMonthMotherBorn"];
    }
    else //retrieve the security question for debug purposes
    {
        throw new ApplicationException($"Unknown security question: {question}!");
    }
    if (string.IsNullOrWhiteSpace(answer))
    {
        throw new ApplicationException($"Couldn't find answer for question '{question}'! Please set answers via env vars, command line args, or appsettings!");
    }
    Console.WriteLine($"Question: {question} -> {new string('*', answer.Length)}");
    if(selectorToSupplyAnswer == "txtAnswer") //text based answer so call Fill
    {
        await page.Locator($"#ctl00_pagePlaceholder_SecurityQuestionUserControl_{selectorToSupplyAnswer}").FillAsync(answer);
    }
    else //set dropdown list with SelectOption
    {
        await page.Locator($"#ctl00_pagePlaceholder_SecurityQuestionUserControl_{selectorToSupplyAnswer}").SelectOptionAsync(new[] { answer });
    }
    Console.WriteLine("Finished setting answer");
}