using System;
using System.Net;
using System.Web;
using System.Web.Http;

using Rock;
using Rock.Data;
using Rock.Model;
using Rock.Rest;
using Rock.Rest.Filters;
using Rock.Utility;

namespace com.BaysideChurch.BinaryFilesNoResize
{
    /// <summary>
    /// REST API controller for uploading binary files with optional image resize control.
    /// This endpoint mirrors Rock's standard BinaryFiles/Upload but allows callers to
    /// skip the automatic 1024x768 image resize by passing resizeIfImage=false.
    /// </summary>
    [Rock.SystemGuid.RestControllerGuid( "E2FBCE4D-FDBF-46A1-A263-07A910F1E64A" )]
    public class BinaryFilesNoResizeController : ApiControllerBase
    {
        /// <summary>
        /// Uploads a file to Rock's binary file system with optional image resize control.
        /// </summary>
        /// <param name="binaryFileTypeGuid">The GUID of the BinaryFileType for the upload.</param>
        /// <param name="resizeIfImage">When false, skips the default 1024x768 resize for images. Defaults to true.</param>
        /// <returns>The ID of the newly created BinaryFile on success.</returns>
        /// <remarks>
        /// Returns:
        /// - 201 Created with BinaryFile.Id on success
        /// - 400 Bad Request if binaryFileTypeGuid is invalid or no file is provided
        /// - 500 Internal Server Error for unexpected errors
        /// </remarks>
        [HttpPost]
        [System.Web.Http.Route( "api/BinaryFilesNoResize/Upload" )]
        [Authenticate, Secured]
        [Rock.SystemGuid.RestActionGuid( "1C4A4150-A04B-49A7-8075-B981CC5F54FB" )]
        public IHttpActionResult Upload( Guid binaryFileTypeGuid, bool resizeIfImage = true )
        {
            try
            {
                // Validate that a file was provided
                var httpRequest = HttpContext.Current.Request;
                if ( httpRequest.Files.Count == 0 )
                {
                    return Content( HttpStatusCode.BadRequest, "No file was provided in the request." );
                }

                var uploadedFile = httpRequest.Files[0];
                if ( uploadedFile == null || uploadedFile.ContentLength == 0 )
                {
                    return Content( HttpStatusCode.BadRequest, "The uploaded file is empty." );
                }

                using ( var rockContext = new RockContext() )
                {
                    // Validate the BinaryFileType exists
                    var binaryFileTypeService = new BinaryFileTypeService( rockContext );
                    var binaryFileType = binaryFileTypeService.Get( binaryFileTypeGuid );

                    if ( binaryFileType == null )
                    {
                        return Content( HttpStatusCode.BadRequest,
                            $"Invalid binaryFileTypeGuid: No BinaryFileType found with GUID '{binaryFileTypeGuid}'." );
                    }

                    // Create the BinaryFile entity
                    var binaryFile = new BinaryFile
                    {
                        IsTemporary = false,
                        BinaryFileTypeId = binaryFileType.Id,
                        MimeType = uploadedFile.ContentType,
                        FileName = FileUtilities.ScrubFileName( uploadedFile.FileName ),
                        FileSize = uploadedFile.ContentLength,
                        // KEY DIFFERENCE: Pass resizeIfImage parameter to control image resizing
                        ContentStream = FileUtilities.GetFileContentStream( uploadedFile, resizeIfImage )
                    };

                    // Save the file
                    var binaryFileService = new BinaryFileService( rockContext );
                    binaryFileService.Add( binaryFile );
                    rockContext.SaveChanges();

                    // Return 201 Created with the new file ID
                    return Content( HttpStatusCode.Created, binaryFile.Id );
                }
            }
            catch ( Exception ex )
            {
                // Log the exception to Rock's exception log
                ExceptionLogService.LogException( ex );
                return Content( HttpStatusCode.InternalServerError,
                    $"An unexpected error occurred while uploading the file: {ex.Message}" );
            }
        }
    }
}
