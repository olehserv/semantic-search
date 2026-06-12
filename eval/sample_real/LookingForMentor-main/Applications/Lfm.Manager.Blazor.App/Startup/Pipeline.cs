using Lfm.Common.Blazor.App.Extensions;
using LFM.DataAccess.DB.Core.Types;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace Lfm.Manager.Blazor.App.Startup
{
    public static class Pipeline
    {
        public static void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseErrorsHandler();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            
            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseCachingUser(LfmIdentityRolesEnum.Manager);

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapBlazorHub();
                endpoints.MapFallbackToPage("/_Host");
            });
        }
    }
}