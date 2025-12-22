using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlazoringComponents.Validators
{
    public class FluentValidationValidator : ComponentBase
    {
        [CascadingParameter]
        private EditContext EditContext {get; set;}

        [Parameter]
        public Type ValidatorType { get; set; }

        private IValidator Validator;
        private ValidationMessageStore ValidationMessageStore;

        [Inject]
        private IServiceProvider ServiceProvider { get; set; }

        public override async Task SetParametersAsync(ParameterView parameters)
        {
            EditContext previousEditContext = EditContext;
            Type previousValidatorType = ValidatorType;

            await base.SetParametersAsync(parameters);

            if (EditContext == null)
                throw new NullReferenceException($"{nameof(FluentValidationValidator)} must be placed within an {nameof(EditForm)}");
            if (ValidatorType == null)
                throw new NullReferenceException($"{nameof(ValidatorType)} must be specified.");
            if (!typeof(IValidator).IsAssignableFrom(ValidatorType))
                throw new ArgumentException($"{ValidatorType.Name} must implement {typeof(IValidator).FullName}");
            if (ValidatorType != previousValidatorType)
                ValidatorTypeChanged();
            // If the EditForm.Model changes then we get a new EditContext
            // and need to hook it up
            if (EditContext != previousEditContext)
                EditContextChanged();
        }

        private void ValidatorTypeChanged()
        {
            Validator = (IValidator)ServiceProvider.GetService(ValidatorType);
        }

        void EditContextChanged()
        {
            ValidationMessageStore = new ValidationMessageStore(EditContext);
            HookUpEditContextEvents();
        }

        private void HookUpEditContextEvents()
        {
            EditContext.OnValidationRequested += ValidationRequested;
            EditContext.OnFieldChanged += FieldChanged;
        }

        async void ValidationRequested(object sender, ValidationRequestedEventArgs args)
        {
            ValidationMessageStore.Clear();
            var validationContext =
              new ValidationContext<object>(EditContext.Model);
            ValidationResult result =
              await Validator.ValidateAsync(validationContext);
            AddValidationResult(EditContext.Model, result);
        }

        void AddValidationResult(object model, ValidationResult validationResult)
        {
            foreach (ValidationFailure error in validationResult.Errors)
            {
                var fieldIdentifier = new FieldIdentifier(model, error.PropertyName);
                ValidationMessageStore.Add(fieldIdentifier, error.ErrorMessage);
            }
            EditContext.NotifyValidationStateChanged();
        }

        async void FieldChanged(object sender, FieldChangedEventArgs args)
        {
            FieldIdentifier fieldIdentifier = args.FieldIdentifier;
            ValidationMessageStore.Clear(fieldIdentifier);
            var propertiesToValidate = new string[] { fieldIdentifier.FieldName };
            var fluentValidationContext =
              new ValidationContext<object>(
                instanceToValidate: fieldIdentifier.Model,
                propertyChain: new FluentValidation.Internal.PropertyChain(),
                validatorSelector: new FluentValidation.Internal.MemberNameValidatorSelector(propertiesToValidate)
              );
            ValidationResult result = await Validator.ValidateAsync(fluentValidationContext);
            AddValidationResult(fieldIdentifier.Model, result);
        }
    }
}
