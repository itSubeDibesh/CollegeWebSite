﻿using CodeAppStore.License.EncodeDecodeRepo;
using CodeAppStore.License.LicenseRepo;
using College.Access.IRepository;
using College.Helpers;
using College.Model.DataTransferObject.AppSettingsDto;
using College.Model.DataTransferObject.AuthDto;
using College.Model.DataTransferObject.AuthExtraDto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;
using System.Threading.Tasks;

namespace College.Controllers
{
    public class LoginController : Controller
    {
        private readonly IAuthRepo _authRepo;
        private readonly IEncodeDecode _encode = new EncodeDecode();
        private readonly ILicense _license = new License();

        public LoginController(IAuthRepo authRepo)
        {
            _authRepo = authRepo;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var data = await _authRepo.FetchSettings();
            if (data != null)
            {
                if (!string.IsNullOrWhiteSpace(data.Certificate) && !string.IsNullOrWhiteSpace(data.License))
                {
                    var oldLicenseData =
                        _license.CheckLicenseVerification(data.License, data.Certificate, data.ClientCode,
                            data.ProjectCode);
                    if (oldLicenseData != null)
                    {
                        if (oldLicenseData.IsValid && !oldLicenseData.IsExpired)
                        {
                            var isRememberedToken = HttpContext.Session.GetString("_Remember");
                            if (isRememberedToken != null && isRememberedToken.Replace(@"""", "") == "True")
                            {
                                var userDetails =
                                    HttpContext.Session.GetComplexData<AuthBasicDetailsModelDto>("_Details");
                                if (userDetails != null)
                                    return RedirectToAction("Index", "ControlPanel");
                                HttpContext.Session.SetString("Warning", "Session Expired, Please Login First!");
                            }

                            return View();
                        }

                        HttpContext.Session.SetString("Warning", "License Expired, " + oldLicenseData.Expiry);
                        return RedirectToAction(nameof(License));
                    }

                    HttpContext.Session.SetString("Warning", "License Not Found, Contact Support Team");
                    return RedirectToAction(nameof(Setup));
                }

                HttpContext.Session.SetString("Error", "License Not Found, Contact Support Team");
                return RedirectToAction(nameof(License));
            }

            HttpContext.Session.SetString("Warning", "License Not Found, Contact Support Team");
            return RedirectToAction(nameof(Setup));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Login(LoginModelDto login)
        {
            if (ModelState.IsValid)
                try
                {
                    if (!await _authRepo.IsValidLogInAsyncTask(login))
                    {
                        HttpContext.Session.SetString("Warning", "Invalid email or password!");
                        return RedirectToAction(nameof(Index));
                    }
                    if (login.RememberMe)
                        HttpContext.Session.SetString("_Remember", new string("True"));
                    var userDetails = await _authRepo.BasicUserDetailsAsyncTask(login.Email);
                    HttpContext.Session.SetComplexData("_Details", userDetails);
                    HttpContext.Session.SetString("Success", "Welcome back " + userDetails.FullName);
                    return RedirectToAction("Index", "ControlPanel");
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    HttpContext.Session.SetString("Error", "Problem connecting To server!");
                    return RedirectToAction(nameof(Index));
                }

            HttpContext.Session.SetString("Error", "Input fields might be empty or invalid!");
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public IActionResult Logout()
        {
            HttpContext.Session.Remove("_Details");
            HttpContext.Session.Remove("_Remember");
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Setup()
        {
            var data = await _authRepo.FetchSettings();
            if (data == null)
            {
                HttpContext.Session.SetInt32("_LicenseSet", 0);
                HttpContext.Session.SetInt32("_SuccessSet", 0);
                var model = new AppSettingsModelDto();
                return View(model);
            }

            if (!string.IsNullOrWhiteSpace(data.Certificate) && !string.IsNullOrWhiteSpace(data.License))
            {
                var oldLicenseData =
                    _license.CheckLicenseVerification(data.License, data.Certificate, data.ClientCode,
                        data.ProjectCode);
                if (oldLicenseData != null)
                {
                    if (oldLicenseData.IsValid && !oldLicenseData.IsExpired)
                    {
                        HttpContext.Session.SetInt32("_LicenseSet", 0);
                        HttpContext.Session.SetInt32("_SuccessSet", 0);
                        return RedirectToAction(nameof(Index));
                    }

                    HttpContext.Session.SetString("Warning", "License Expired, " + oldLicenseData.Expiry);
                    return RedirectToAction(nameof(Setup));
                }

                HttpContext.Session.SetString("Warning", "License Not Found, Contact Support Team");
                return RedirectToAction(nameof(Setup));
            }

            HttpContext.Session.SetString("Error", "License Not Found, Contact Support Team");
            return RedirectToAction(nameof(License));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Setup(AppSettingsModelDto settings)
        {
            if (ModelState.IsValid)
            {
                if (!string.IsNullOrWhiteSpace(settings.Email) &&
                    !string.IsNullOrWhiteSpace(settings.OrganizationName))
                {
                    var email = settings.Email.Replace('Z', 'A').Replace('z', 'a').Replace('.', 'a');
                    var clientCode = _license.GenerateClientCode(email);
                    var pCode = "Information Management System " + settings.OrganizationName +
                                " CodeAppStore".Replace('z', 'a').Replace('Z', 'a').Replace('a', 'a');
                    var projectCode = _license.GenerateProjectCode(pCode);
                    var combinedCode = clientCode + "." + projectCode;
                    var encryptCombinedCode = _encode.Encrypt(combinedCode);
                    var settingToAdd = new AppSettingsModelDto
                    {
                        Email = settings.Email,
                        ProjectCode = projectCode,
                        ClientCode = clientCode,
                        OrganizationName = settings.OrganizationName
                    };
                    if (await _authRepo.AddAppSettings(settingToAdd))
                    {
                        var downloadInfo =
                            "------------------------------------------------------------------------------\n" +
                            "            CodeAppStore   \n" +
                            "        Information Management System support file   \n" +
                            "------------------------------------------------------------------------------\n" +
                            "Please Send this file to support team without editing any thing," +
                            "as your License Support Depends on this file.\n" +
                            "Contact : 9861315234, 9823113741\n" +
                            "Email   : dibeshrsubedi@gmail.com, dibesh@codeappstore.com\n" +
                            "BCC     : cullaapar@gmail.com, apaar@codeappstore.com\n" +
                            "------------------------------------------------------------------------------\n" +
                            $"Organization Name : {settings.OrganizationName} \n" +
                            $"Email             : {settings.Email} \n" +
                            $"Unique Key        : {encryptCombinedCode} \n" +
                            $"Date              : {DateTime.Now.ToShortDateString()}\n" +
                            "------------------------------------------------------------------------------\n";
                        var memoryStream = new MemoryStream();
                        TextWriter tw = new StreamWriter(memoryStream);
                        await tw.WriteLineAsync(downloadInfo);
                        await tw.FlushAsync();
                        tw.Close();
                        HttpContext.Session.SetInt32("_SuccessSet", 1);
                        HttpContext.Session.SetInt32("_LicenseSet", 0);
                        HttpContext.Session.SetString("Success",
                            "Almost Done, Send the Information_Management_System_Support.txt to support team!");
                        return File(memoryStream.GetBuffer(), "text/plain",
                            "Information_Management_System_Support.txt");
                    }

                    HttpContext.Session.SetString("Error", "Setting Up Application, Try Again!");
                    return RedirectToAction(nameof(Setup));
                }

                HttpContext.Session.SetString("Error", "Input fields might be empty or invalid!");
                return RedirectToAction(nameof(Setup));
            }

            HttpContext.Session.SetString("Error", "Input fields might be empty or invalid!");
            return RedirectToAction(nameof(Setup));
        }

        [HttpGet]
        public async Task<IActionResult> License()
        {
            var data = await _authRepo.FetchSettings();
            if (data == null)
            {
                HttpContext.Session.SetInt32("_LicenseSet", 0);
                return RedirectToAction(nameof(Setup));
            }

            if (!string.IsNullOrWhiteSpace(data.Certificate) && !string.IsNullOrWhiteSpace(data.License))
            {
                var oldLicenseData =
                    _license.CheckLicenseVerification(data.License, data.Certificate, data.ClientCode,
                        data.ProjectCode);
                if (oldLicenseData != null)
                {
                    if (oldLicenseData.IsValid && !oldLicenseData.IsExpired)
                    {
                        HttpContext.Session.SetInt32("_LicenseSet", 0);
                        HttpContext.Session.SetInt32("_SuccessSet", 1);
                        return RedirectToAction(nameof(Index));
                    }

                    HttpContext.Session.SetString("Warning", "License Expired, " + oldLicenseData.Expiry);
                    return RedirectToAction(nameof(License));
                }

                HttpContext.Session.SetString("Warning", "License Not Found, Contact Support Team");
                return RedirectToAction(nameof(License));
            }

            HttpContext.Session.SetString("Error", "License Not Found, Contact Support Team");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> License(string license)
        {
            if (!string.IsNullOrWhiteSpace(license))
            {
                var input = license;
                var data = await _authRepo.FetchSettings();
                if (data != null)
                {
                    var clientCode = data.ClientCode;
                    var projectCode = data.ProjectCode;
                    var lengthOfCombinedLicense = input.Length;
                    var indexOfDot = Convert.ToInt32(input.LastIndexOf('.'));
                    var letterCountOfCertificate = lengthOfCombinedLicense - indexOfDot;
                    var extractedLicense = input.Substring(0, indexOfDot);
                    var extractedCertificate = input.Substring(indexOfDot, letterCountOfCertificate).Replace(".", "");

                    if (string.IsNullOrWhiteSpace(extractedCertificate) && string.IsNullOrWhiteSpace(extractedLicense))
                    {
                        HttpContext.Session.SetInt32("_LicenseSet", 0);
                        HttpContext.Session.SetString("Error", "Invalid License Key");
                        return RedirectToAction(nameof(License));
                    }

                    var oldLicenseData = _license.CheckLicenseVerification(extractedLicense, extractedCertificate,
                        clientCode, projectCode);
                    if (oldLicenseData.IsValid)
                    {
                        var result = await _authRepo.UpdateAppSettings(extractedLicense, extractedCertificate);
                        if (result)
                        {
                            HttpContext.Session.SetInt32("_LicenseSet", 1);
                            HttpContext.Session.SetString("Success", "License Updated Successfully.");
                            return RedirectToAction(nameof(Index));
                        }

                        HttpContext.Session.SetInt32("_LicenseSet", 0);
                        HttpContext.Session.SetString("Error", "Problem updating License.");
                        return RedirectToAction(nameof(License));
                    }

                    HttpContext.Session.SetString("Error", "Invalid License, Please Provide a valid license.");
                    return RedirectToAction(nameof(License));
                }

                HttpContext.Session.SetString("Error", "Contact Support Team, Problem Verifying License!");
                return RedirectToAction(nameof(License));
            }

            HttpContext.Session.SetString("Error", "Input fields might be empty or invalid!");
            return RedirectToAction(nameof(License));
        }
    }
}