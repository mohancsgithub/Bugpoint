﻿using AutoMapper;
using BugPoint.Application.Filters;
using BugPoint.Application.Notification;
using BugPoint.Common;
using BugPoint.Data.Masters.Command;
using BugPoint.Data.Masters.Queries;
using BugPoint.Model.GeneralSetting;
using BugPoint.Services.MailHelper;
using BugPoint.ViewModel.GeneralSettings;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace BugPoint.Application.Areas.Administration.Controllers
{
    [Area("Administration")]
    [SessionTimeOut]
    [AuthorizeSuperAdmin]
    [ServiceFilter(typeof(AuditFilterAttribute))]
    public class GeneralConfigurationController : Controller
    {
        private readonly ISmtpSettingsCommand _smtpSettingsCommand;
        private readonly IMapper _mapper;
        private readonly INotificationService _notificationService;
        private readonly ISmtpSettingsQueries _smtpSettingsQueries;
        private readonly IMailingService _mailingService;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly ILogger<MailingService> _logger;
        public GeneralConfigurationController(
            ISmtpSettingsCommand smtpSettingsCommand,
            IMapper mapper,
            INotificationService notificationService,
            ISmtpSettingsQueries smtpSettingsQueries, IMailingService mailingService, IWebHostEnvironment webHostEnvironment, ILogger<MailingService> logger)
        {
            _smtpSettingsCommand = smtpSettingsCommand;
            _mapper = mapper;
            _notificationService = notificationService;
            _smtpSettingsQueries = smtpSettingsQueries;
            _mailingService = mailingService;
            _webHostEnvironment = webHostEnvironment;
            _logger = logger;
        }

        [HttpGet]
        public ActionResult SmtpSettings()
        {
            var smtpModels = new SmtpEmailSettingsViewModel()
            {
                SslProtocol = "N",
                TlSProtocol = "N"
            };

            return View(smtpModels);
        }

        [HttpPost]
        public ActionResult SmtpSettings(SmtpEmailSettingsViewModel smtpModel)
        {
            try
            {
                if (ModelState.IsValid)
                {

                    var smtpEmailSettings = _mapper.Map<SmtpEmailSettingsModel>(smtpModel);
                    smtpEmailSettings.UserId = HttpContext.Session.GetInt32(AllSessionKeys.UserId);
                    smtpEmailSettings.CreatedOn = DateTime.Now;
                    smtpEmailSettings.SmtpProviderId = 0;

                    if (!string.IsNullOrEmpty(smtpModel.Password))
                    {
                        AesAlgorithm aesAlgorithm = new AesAlgorithm();
                        var byteencrypt = aesAlgorithm.EncryptToByte(smtpModel.Password);
                        string password = Encoding.UTF8.GetString(byteencrypt);
                        smtpEmailSettings.Password = password;
                    }
                    _smtpSettingsCommand.SaveSmtpSettings(smtpEmailSettings);
                    _notificationService.SuccessNotification("Message", "SMTP Settings Saved Successfully");

                    return RedirectToAction("AllSettings", "GeneralConfiguration");
                }
                return View(smtpModel);
            }
            catch (Exception)
            {
                throw;
            }
        }

        [HttpGet]
        public ActionResult AllSettings()
        {
            return View();
        }

        [HttpPost]
        public IActionResult GetAllSmtpSettings()
        {
            try
            {
                try
                {
                    var draw = HttpContext.Request.Form["draw"].FirstOrDefault();
                    var start = Request.Form["start"].FirstOrDefault();
                    var length = Request.Form["length"].FirstOrDefault();
                    var sortColumn = Request.Form["columns[" + Request.Form["order[0][column]"].FirstOrDefault() + "][name]"].FirstOrDefault();
                    var sortColumnDirection = Request.Form["order[0][dir]"].FirstOrDefault();
                    var searchValue = Request.Form["search[value]"].FirstOrDefault();
                    int pageSize = length != null ? Convert.ToInt32(length) : 0;
                    int skip = start != null ? Convert.ToInt32(start) : 0;
                    int recordsTotal = 0;
                    var smtpdata = _smtpSettingsQueries.ShowAllEmailSettings(sortColumn, sortColumnDirection, searchValue);
                    recordsTotal = smtpdata.Count();
                    var data = smtpdata.Skip(skip).Take(pageSize).ToList();
                    var jsonData = new { draw = draw, recordsFiltered = recordsTotal, recordsTotal = recordsTotal, data = data };
                    return Ok(jsonData);
                }
                catch (Exception)
                {
                    throw;
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        [HttpGet]
        public ActionResult EditSmtpSettings(int? id)
        {
            if (id != null)
            {
                var smtpdata = _smtpSettingsQueries.EditSmtpSettings(id);
                return View(smtpdata);
            }

            return RedirectToAction("AllSettings", "GeneralConfiguration");
        }


        [HttpPost]
        public ActionResult EditSmtpSettings(SmtpEmailSettingsViewModel smtpModel)
        {
            try
            {
                if (ModelState.IsValid)
                {

                    var smtpdata = _smtpSettingsQueries.SmtpSettings(smtpModel.SmtpProviderId);
                    smtpdata.Host = smtpModel.Host;
                    smtpdata.Port = smtpModel.Port;
                    smtpdata.Timeout = smtpModel.Timeout;
                    smtpdata.SslProtocol = smtpModel.SslProtocol;
                    smtpdata.TlSProtocol = smtpModel.TlSProtocol;
                    smtpdata.Username = smtpModel.Username;

                    if (!string.IsNullOrEmpty(smtpModel.Password))
                    {
                        AesAlgorithm aesAlgorithm = new AesAlgorithm();
                        var password = aesAlgorithm.EncryptToBase64String(smtpModel.Password);
                        smtpdata.Password = password;
                    }

                    smtpdata.MailSender = smtpModel.MailSender;
                    smtpdata.ModifiedOn = DateTime.Now;
                    _smtpSettingsCommand.UpdateSmtpSettings(smtpdata);
                    TempData["MessageSmtp"] = "SMTP Setting Save Successfully!";

                    return RedirectToAction("AllSettings", "GeneralConfiguration");
                }
                return View(smtpModel);
            }
            catch (Exception)
            {
                throw;
            }
        }

        [HttpPost]
        public JsonResult SetDefaultConnection(RequestSmtp requestSmtp)
        {
            try
            {
                if (requestSmtp.SmtpProviderId != null)
                {
                    var result = _smtpSettingsCommand.SettingDefaultConnection(requestSmtp.SmtpProviderId);

                    if (result > 0)
                    {
                        return Json(new { status = "Success" });
                    }
                    else
                    {
                        return Json(new { status = "Failed" });
                    }
                }
                else
                {
                    return Json(new { status = "Failed" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Set Default Connection Failed");
                throw;
            }
        }

        [HttpPost]
        public JsonResult TestConnection(RequestSmtp requestSmtp)
        {
            try
            {

                var smtpdata = _smtpSettingsQueries.SmtpSettings(requestSmtp.SmtpProviderId);
                var result = CreateVerificationEmail(smtpdata);
                if (result == "success")
                {
                    return Json(new { status = "Success" });
                }
                else
                {
                    return Json(new { status = "Failed" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Test Connection Failed");
                return Json(new { status = "Failed" });
            }
        }

        private string CreateVerificationEmail(SmtpEmailSettingsModel smtpEmailSettingsModel)
        {
            try
            {
                var filePath = Path.Combine(_webHostEnvironment.WebRootPath, "templates", "forgotpassword.html");

                string mailText;
                using (var streamReader = new StreamReader(filePath))
                {
                    mailText = streamReader.ReadToEnd();
                    streamReader?.Close();
                }

                var stringtemplate = new StringBuilder();
                stringtemplate.Append("<strong> Test Email </strong>");
                stringtemplate.Append("<br/>");
                stringtemplate.Append("Welcome to Bugpoint.");
                stringtemplate.Append("Author :- Saineshwar Bageri");
                stringtemplate.Append("<br/>");
                mailText = mailText.Replace("[XXXXXXXXXXXXXXXXXXXXX]", stringtemplate.ToString());
                mailText = mailText.Replace("[##Name##]", $"SuperAdmin");

                var sendingMailRequest = new SendingMailRequest()
                {
                    Attachments = null,
                    ToEmail = smtpEmailSettingsModel.EmailTo,
                    Body = mailText,
                    Subject = $"Test Email From New SMTP"
                };


                var result = _mailingService.SendEmailAsync(sendingMailRequest);

                return "success";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SendTestEmail Create Verification Email  ");
                return "failed";
            }
        }


    }
}
