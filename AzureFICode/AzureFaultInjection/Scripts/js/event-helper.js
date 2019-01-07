$(document).ready(function () {
    getAzureOperations();
    getTenantInformation();
});

function getTenantInformation() {
    var request = $.ajax({
        url: "api/FaultInjection/gettenantinformation",
        type: "GET"
    });

    request.done(function (result) {
        if (!result) {
            console.log("azure operation list is empty");
            return;
        }

        if (result.TenantId && result.ApplicationId) {
            $('#tenant-id').val(result.TenantId);
            $('#tenant-id').attr("readonly", true);
            $('#client-id').attr("readonly", true);
            $('#client-id').val(result.ApplicationId);
        }
    });

    request.fail(function (jqXHR, textStatus) {
        alert("Request failed: " + textStatus);
    });
}

function getSubsId(id) {
    if (id && id.length > 2) {
        return id.split('/')[2];
    }
}

$("#reset").on("click", function (e) {
    getTenantInformation();
});
$("#submit").on("click", function (e) {
    e.preventDefault();
    var values = {};
    var formElement = $('#multiwizard');
    $.each(formElement.serializeArray(), function (i, field) {
        if (field.value === 'on') {
            field.value = true;
        }
        if (field.value === 'off') {
            field.value = false;
        }
        if (field.name === 'isFaultOrUpdateDomainEnabled') {
            var faultDomain = $("#isFaultDomainEnabled")[0].checked;
            var updateDomain = $("#isUpdateDomainEnabled")[0].checked;
            if (faultDomain) {
                values["isFaultDomainEnabled"] = faultDomain;
            }
            if (updateDomain) {
                values["isUpdateDomainEnabled"] = updateDomain;
            }
        }
        if (field.name === 'isAzureFiStartedOrStopped') {
            var azureFiStarted = $("#isAzureFiStarted")[0].checked;
            var azureFiStopped = $("#isAzureFiStopped")[0].checked;
            if (azureFiStarted) {
                values["isChaosEnabled"] = true;
            }
            if (azureFiStopped) {
                values["isChaosEnabled"] = false;
            }
        }
        if (field.name === 'subscription') {
            values[field.name] = getSubsId(field.value);
        }
        else if (field.name === 'azureFiActions') {
            values[field.name] = $("#azure-fault-injection-actions").val();
        }
        else if (field.name === 'excludedResourceGroups') {
            values[field.name] = $("#excluded-resource-groups").val();
        } else {
            values[field.name] = field.value;
        }
    });


    var request = $.ajax({
        url: "api/FaultInjection/createblob",
        type: "POST",
        data: values,
        beforeSend: function (xhr, options) {
            if (2 * values["rollbackFrequency"] > values["schedulerFrequency"]) {
                alert(
                    "Power Cycle Schedule Frequency should be less than half the Scheduler Frequency, Please decrease the Power Cycle Schedule or increase the Scheduler Frequency value");
                xhr.abort();
            } else if (2 * values["rollbackFrequency"] <= values["schedulerFrequency"] &&
                values["schedulerFrequency"] > values["crawlerFrequency"] + values["meanTime"]) {
                alert(
                    "Scheduler Frequency should be less than the sum of Crawler Frequency & Mean Time between FI on VM, Please reduce the value of Scheduler Frequency");
                xhr.abort();

            } else {
                $(".modal").show();
            }
        },
        success: function (response) {
            if (response || response.Success || response.SuccessMessage) {
                alert(response.SuccessMessage);
                return;
            }
        },
        complete: function () {
            $(".modal").hide();
        },
        error: function () {
            $(".modal").hide();
        }
    });
    //request.done(function (response) {
    //    if (response || response.Success || response.SuccessMessage) {
    //        alert(response.SuccessMessage);
    //        return;
    //    }
    //    else if (response && response.ErrorMessage) {
    //        alert(response.ErrorMessage);
    //    }
    //    else {
    //        alert("Something went wrong, Please check valid permissions are given on ADD or Please try again later!");
    //    }
    //});

    //request.fail(function (jqXhr, textStatus, errorThrown) {
    //    alert("Request failed: " + errorThrown);
    //});
});

$("#avset-enabled").change(function () {
    var isEnabled = this.checked;
    if (!isEnabled) {
        $("#isFaultDomainEnabled")[0].checked = false;
        $("#isUpdateDomainEnabled")[0].checked = false;
    }
});

$("#selectSubscription").change(function () {
    var subscription = this.value;
    getResourceGroups(subscription);
});



function getSubscriptions(currentParent, nextParent, callback) {
    var tenantId = currentParent.find("#tenant-id").val();
    var clientId = currentParent.find("#client-id").val();
    var clientSecret = currentParent.find("#client-secret").val();
    if (!tenantId || !clientId || !clientSecret) {
        return false;
    }

    var request = $.ajax({
        url: "api/FaultInjection/getsubscriptions",
        type: "GET",
        async: false,
        data: { tenantId: tenantId, clientId: clientId, clientSecret: clientSecret },
        beforeSend: function () {
            $(".modal").show();
        },
        complete: function () {
            $(".modal").hide();
        },
        error: function () {
            $(".modal").hide();
        }
    });
    request.done(function (response) {
        if (!response) {
            alert("Something went wrong, please try again later!");
            callback(currentParent, nextParent, false);
        }

        console.log("Resource Group Name: " + response.ResourceGroup)
        console.log("Storage Account Name: " + response.StorageAccount)
        if (response.Success === false || !response.Result) {
            console.log("subscription list is empty");
            if (response.ErrorMessage) {
                alert(response.ErrorMessage);
            }

            callback(currentParent, nextParent, false);
        }

        var result = response.Result;
        bindOptions($('#selectSubscription'), result.SubcriptionList);
        $('#selectSubscription').SumoSelect();
        $("#submit").val("Configure and Deploy");
        $("#submit").text("Configure and Deploy");
        if (result.Config) {
            $("#submit").val("Update Configuration");
            $("#submit").text("Update Configuration");
            $("#selectSubscription")[0].sumo.selectItem(result.Config.subscription);
            bindExistingConfig(result.Config, result.ResourceGroups)
        }
        else {
            getResourceGroups(result.SubcriptionList[0].id);
        }

        callback(currentParent, nextParent, true);
    });

    request.fail(function (jqXHR, textStatus) {
        alert("Request failed: " + textStatus);
        callback(currentParent, nextParent, false);
    });
}

function getResourceGroups(subscription) {
    var tenantId = $.find("#tenant-id")[0].value;
    var clientId = $.find("#client-id")[0].value;
    var clientSecret = $.find("#client-secret")[0].value;
    var request = $.ajax({
        url: "api/FaultInjection/getresourcegroups",
        type: "GET",
        data: { tenantId: tenantId, clientId: clientId, clientSecret: clientSecret, subscription: subscription }
    });
    request.done(function (result) {
        if (!result) {
            console.log("resource group list is empty");
            return;
        }
        console.log("InItlize Multi list");

        bindOptions($('#excluded-resource-groups'), result);
        bindOptions($('#included-resource-groups'), result);
        $('#excluded-resource-groups').SumoSelect({ selectAll: true });
        $('#included-resource-groups').SumoSelect({ selectAll: true });
    });

    request.fail(function (jqXHR, textStatus) {
        alert("Request failed: " + textStatus);
    });
}

function getAzureOperations() {
    var request = $.ajax({
        url: "api/FaultInjection/getazureoperations",
        type: "GET"
    });

    request.done(function (result) {
        if (!result) {
            console.log("azure operation list is empty");
            return;
        }

        bindDictionaryOptions($('#azure-fault-injection-actions'), result);
        $('#azure-fault-injection-actions').SumoSelect({ selectAll: true });
    });

    request.fail(function (jqXHR, textStatus) {
        alert("Request failed: " + textStatus);
    });
}

function bindExistingConfig(model, resourceGroups) {
    if (resourceGroups) {
        bindOptions($('#excluded-resource-groups'), resourceGroups);
        $('#excluded-resource-groups').SumoSelect({ selectAll: true });
        $('#azure-fault-injection-actions').SumoSelect({ selectAll: true });
    }

    if (model) {
        selectItem(model.SubcriptionList, '#selectSubscription');
        selectItem(model.excludedResourceGroups, '#excluded-resource-groups');
        selectItem(model.azureFiActions, '#azure-fault-injection-actions');
        $("#vm-percentage")[0].value = model.vmPercentage;
        $("#vm-enabled")[0].checked = model.isVmEnabled;
        $("#avset-enabled")[0].checked = model.isAvSetEnabled;
        $("#isFaultDomainEnabled")[0].checked = model.isFaultDomainEnabled;
        $("#isUpdateDomainEnabled")[0].checked = model.isUpdateDomainEnabled;
        $("#avzone-enabled")[0].checked = model.isAvZoneEnabled;
        $("#vmss-percentage")[0].value = model.vmssPercentage;
        $("#vmss-enabled")[0].checked = model.isVmssEnabled;
        $("#loadbalancer-percentage")[0].value = model.loadbalancerPercentage;
        $("#loadbalancer-enabled")[0].checked = model.isLoadbalancerEnabled;
        $("#scheduler-frequency")[0].value = model.schedulerFrequency;
        $("#rollback-frequency")[0].value = model.rollbackFrequency;
        $("#crawler-frequency")[0].value = model.crawlerFrequency;
        $("#mean-time")[0].value = model.meanTime;
        if (model.isChaosEnabled) {
            $("#isAzureFiStarted")[0].checked = model.isChaosEnabled;
        }
        else {
            $("#isAzureFiStopped")[0].checked = true;
        }
    }
}

function selectItem(needsToBeSelected, selector) {
    if (selector) {
        $.each(needsToBeSelected, function (index, value) {
            $(selector)[0].sumo.selectItem(value);
        });
    }
}

function bindDictionaryOptions($element, result) {
    $element.empty();
    $.each(result, function (index, item) {
        $element.append(
            $('<option/>', {
                value: index,
                text: item
            })
        );
    });
}

function bindOptions($element, result) {
    $element.empty();
    $.each(result, function (index, item) {
        $element.append(
            $('<option/>', {
                value: item.id,
                text: item.displayName ? item.displayName : item.name
            })
        );
    });
}