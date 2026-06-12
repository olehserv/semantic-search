$('.toast').toast('show');

var readURL = function(input) {
    if (input.files && input.files[0]) {
        var reader = new FileReader();
        reader.onload = function(e) {
            $(".profile-pic").attr("src", e.target.result);
        };
        reader.readAsDataURL(input.files[0]);
    }
};

$(".file-upload").on("change", function() {
    readURL(this);
});

$(".upload-button").on("click", function(e) {
    e.preventDefault();
    $(".file-upload").click();
});

$(document).ready(function(){

    $('#smartwizard').smartWizard({
        selected: 0,
        theme: 'dots',
        autoAdjustHeight:true,
        transitionEffect:'fade',
        showStepURLhash: false,
    });

});