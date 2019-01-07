/**
 * @name Multi-step form - WIP
 * @description Prototype for basic multi-step form
 * @deps jQuery, jQuery Validate
 */

var app = {

  init: function () {
    this.cacheDOM();
    this.setupAria();
    this.nextButton();
    this.prevButton();
    this.validateForm();
    this.startOver();
    // this.editForm();
    this.killEnterKey();
    // this.handleStepClicks();
  },

  cacheDOM: function () {
    if ($(".multi-step-form").size() === 0) { return; }
    this.$formParent = $(".multi-step-form");
    this.$form = this.$formParent.find("form");
    this.$formStepParents = this.$form.find("fieldset"),

      this.$nextButton = this.$form.find(".btn-next");
    this.$prevButton = this.$form.find(".btn-prev");
    this.$editButton = this.$form.find(".btn-edit");
    this.$resetButton = this.$form.find("[type='reset']");

    this.$stepsParent = $(".steps");
    this.$steps = this.$stepsParent.find("li");
  },

  htmlClasses: {
    activeClass: "active",
    hiddenClass: "hidden",
    visibleClass: "visible",
    editFormClass: "edit-form",
    animatedVisibleClass: "animated fadeIn",
    animatedHiddenClass: "animated fadeOut",
    animatingClass: "animating"
  },

  setupAria: function () {
    if (!this.$formStepParents) {
      return;
    }

    // set first parent to visible
    this.$formStepParents.eq(0).attr("aria-hidden", false);

    // set all other parents to hidden
    this.$formStepParents.not(":first").attr("aria-hidden", true);

    // handle aria-expanded on next/prev buttons
    app.handleAriaExpanded();

  },

  nextButton: function () {

    if (!this.$nextButton) {
      return;
    }
    this.$nextButton.on("click", function (e) {

      e.preventDefault();

      // grab current step and next step parent
      var $this = $(this),
        currentParent = $this.closest("fieldset"),
        nextParent = currentParent.next();

      // if the form is valid hide current step
      // trigger next step

      if (app.checkForValidForm()) {
        app.isValid(currentParent, nextParent);
      }
    });
  },
  moveNext: function (currentParent, nextParent, validToMove) {
    if (validToMove && currentParent) {
      currentParent.removeClass(app.htmlClasses.visibleClass);
      app.showNextStep(currentParent, nextParent);
    }
  },

  prevButton: function () {
    if (!this.$prevButton) {
      return;
    }

    this.$prevButton.on("click", function (e) {

      e.preventDefault();

      // grab current step parent and previous parent
      var $this = $(this),
        currentParent = $(this).closest("fieldset"),
        prevParent = currentParent.prev();

      // hide current step and show previous step
      // no need to validate form here
      currentParent.removeClass(app.htmlClasses.visibleClass);
      app.showPrevStep(currentParent, prevParent);

    });
  },

  showNextStep: function (currentParent, nextParent) {
    if (!currentParent || !nextParent) {
      return;
    }
    // hide previous parent
    currentParent
      .addClass(app.htmlClasses.hiddenClass)
      .attr("aria-hidden", true);

    // show next parent
    nextParent
      .removeClass(app.htmlClasses.hiddenClass)
      .addClass(app.htmlClasses.visibleClass)
      .attr("aria-hidden", false);

    // focus first input on next parent
    nextParent.focus();

    // activate appropriate step
    app.handleState(nextParent.index());

    // handle aria-expanded on next/prev buttons
    app.handleAriaExpanded();

  },

  showPrevStep: function (currentParent, prevParent) {

    // hide previous parent
    currentParent
      .addClass(app.htmlClasses.hiddenClass)
      .attr("aria-hidden", true);

    // show next parent
    prevParent
      .removeClass(app.htmlClasses.hiddenClass)
      .addClass(app.htmlClasses.visibleClass)
      .attr("aria-hidden", false);

    // send focus to first input on next parent
    prevParent.focus();

    // activate appropriate step
    app.handleState(prevParent.index());

    // handle aria-expanded on next/prev buttons
    app.handleAriaExpanded();

  },

  handleAriaExpanded: function () {

		/*
			Loop thru each next/prev button
			Check to see if the parent it conrols is visible
			Handle aria-expanded on buttons
		*/
    $.each(this.$nextButton, function (idx, item) {
      var controls = $(item).attr("aria-controls");
      if ($("#" + controls).attr("aria-hidden") == "true") {
        $(item).attr("aria-expanded", false);
      } else {
        $(item).attr("aria-expanded", true);
      }
    });

    $.each(this.$prevButton, function (idx, item) {
      var controls = $(item).attr("aria-controls");
      if ($("#" + controls).attr("aria-hidden") == "true") {
        $(item).attr("aria-expanded", false);
      } else {
        $(item).attr("aria-expanded", true);
      }
    });

  },

  validateForm: function () {
    if (!this.$form) {
      return;
    }

    // jquery validate form validation
    this.$form.validate({
      ignore: ":hidden", // any children of hidden desc are ignored
      errorElement: "span", // wrap error elements in span not label
      invalidHandler: function (event, validator) { // add aria-invalid to el with error
        $.each(validator.errorList, function (idx, item) {
          if (idx === 0) {
            $(item.element).focus(); // send focus to first el with error
          }
          $(item.element).attr("aria-invalid", true); // add invalid aria
        })
      },
      submitHandler: function (form) {
        alert("form submitted!");
        // form.submit();
      }
    });
  },

  checkForValidForm: function () {
    if (this.$form.valid()) {
      return true;
    }
  },

  startOver: function () {
    if (!this.$formStepParents) {
      return;
    }

    var $parents = this.$formStepParents,
      $firstParent = this.$formStepParents.eq(0),
      $formParent = this.$formParent,
      $stepsParent = this.$stepsParent;

    this.$resetButton.on("click", function (e) {

      // hide all parents - show first
      $parents
        .removeClass(app.htmlClasses.visibleClass)
        .addClass(app.htmlClasses.hiddenClass)
        .eq(0).removeClass(app.htmlClasses.hiddenClass)
        .eq(0).addClass(app.htmlClasses.visibleClass);

      // remove edit state if present
      $formParent.removeClass(app.htmlClasses.editFormClass);

      // manage state - set to first item
      app.handleState(0);

      // reset stage for initial aria state
      app.setupAria();

      // send focus to first item
      setTimeout(function () {
        $firstParent.focus();
      }, 200);

    }); // click

  },

  handleState: function (step) {

    this.$steps.eq(step).prevAll().removeClass(app.htmlClasses.activeClass);
    this.$steps.eq(step).addClass(app.htmlClasses.activeClass);

    // restart scenario
    if (step === 0) {
      this.$steps
        .removeClass(app.htmlClasses.activeClass)
      // .attr("disabled", "disabled");
      this.$steps.eq(0).addClass(app.htmlClasses.activeClass)
    }
  },

  editForm: function () {
    var $formParent = this.$formParent,
      $formStepParents = this.$formStepParents,
      $stepsParent = this.$stepsParent;

    this.$editButton.on("click", function () {
      $formParent.toggleClass(app.htmlClasses.editFormClass);
      $formStepParents.attr("aria-hidden", false);
      $formStepParents.eq(0).find("input").eq(0).focus();
      app.handleAriaExpanded();
    });
  },

  killEnterKey: function () {
    $(document).on("keypress", ":input:not(textarea,button)", function (event) {
      return event.keyCode != 13;
    });
  },

  handleStepClicks: function () {

    var $stepTriggers = this.$steps,
      $stepParents = this.$formStepParents;

    if (!$stepTriggers) {
      return;
    }

    $stepTriggers.on("click", function (e) {

      e.preventDefault();

      var btnClickedIndex = $(this).index();

      // kill active state for items after step trigger
      $stepTriggers.nextAll()
        .removeClass(app.htmlClasses.activeClass)
      //.attr("disabled", true);

      // activate button clicked
      $(this)
        .addClass(app.htmlClasses.activeClass)
      // .attr("disabled", false)

      // hide all step parents
      $stepParents
        .removeClass(app.htmlClasses.visibleClass)
        .addClass(app.htmlClasses.hiddenClass)
      // .attr("aria-hidden", true);

      // show step that matches index of button
      $stepParents.eq(btnClickedIndex)
        .removeClass(app.htmlClasses.hiddenClass)
        .addClass(app.htmlClasses.visibleClass)
        .attr("aria-hidden", false)
        .focus();

    });

  },

  isValid(currentParent, nextParent) {
    if (currentParent.attr("id") === "step-1") {
      return app.step1Validation(currentParent, nextParent);
    }
    else if (currentParent.attr("id") === "step-3") {
      return this.moveNext(currentParent, nextParent, app.step3Validation(currentParent, nextParent));
    }

    this.moveNext(currentParent, nextParent, true);
  },

  // Specific steps validation
  step1Validation: function (currentParent, nextParent) {
    if (!currentParent) {
      return false;
    }

    return getSubscriptions(currentParent, nextParent, this.moveNext);
  },

  step3Validation: function (currentParent) {
    if (!currentParent) {
      return false;
    }

    var avsetsEnabled = currentParent.find("#avset-enabled")[0].checked;
    var faultDomainEnabled = currentParent.find("#isFaultDomainEnabled")[0].checked;
    var updateDomainEnabled = currentParent.find("#isUpdateDomainEnabled")[0].checked;
    if (avsetsEnabled) {
      if (!faultDomainEnabled && !updateDomainEnabled) {
        alert("Please select either fault domain or update domain");
        return (faultDomainEnabled || updateDomainEnabled);
      }
    }
    else if (faultDomainEnabled || updateDomainEnabled) {
      if (!avsetsEnabled) {
        alert("Please enable the available sets azure fi");
        return false;
      }
    }

    return true;
  }
};

app.init();