/*
 * libdeflate.h - public header for libdeflate
 */

#ifndef LIBDEFLATE_H
#define LIBDEFLATE_H

#ifdef __cplusplus
extern "C" {
#endif

#define LIBDEFLATE_VERSION_MAJOR	0
#define LIBDEFLATE_VERSION_MINOR	8
#define LIBDEFLATE_VERSION_STRING	"0.8"

#include <stddef.h>
#include <stdint.h>

/*
 * On Windows, if you want to link to the DLL version of libdeflate, then
 * #define LIBDEFLATE_DLL.
 */
#ifdef LIBDEFLATE_DLL
#  ifdef BUILDING_LIBDEFLATE
#    define LIBDEFLATEAPI LIBEXPORT
#  elif defined(_WIN32) || defined(__CYGWIN__)
#    define LIBDEFLATEAPI __declspec(dllimport)
#  endif
#endif
#ifndef LIBDEFLATEAPI
#  define LIBDEFLATEAPI
#endif

/* ========================================================================== */
/*                             Compression                                    */
/* ========================================================================== */

struct libdeflate_compressor;

/*
 * libdeflate_alloc_compressor() allocates a new compressor that supports
 * DEFLATE, zlib, and gzip compression.  'compression_level' is the compression
 * level on a zlib-like scale but with a higher maximum value (1 = fastest, 6 =
 * medium/default, 9 = slow, 12 = slowest).  The return value is a pointer to
 * the new compressor, or NULL if out of memory.
 *
 * Note: for compression, the sliding window size is defined at compilation time
 * to 32768, the largest size permissible in the DEFLATE format.  It cannot be
 * changed at runtime.
 *
 * A single compressor is not safe to use by multiple threads concurrently.
 * However, different threads may use different compressors concurrently.
 */
LIBDEFLATEAPI struct libdeflate_compressor *
libdeflate_alloc_compressor(int compression_level);

/*
 * libdeflate_deflate_compress() performs raw DEFLATE compression on a buffer of
 * data.  The function attempts to compress 'in_nbytes' bytes of data located at
 * 'in' and write the results to 'out', which has space for 'out_nbytes_avail'
 * bytes.  The return value is the compressed size in bytes, or 0 if the data
 * could not be compressed to 'out_nbytes_avail' bytes or fewer.
 */
LIBDEFLATEAPI size_t
libdeflate_deflate_compress(struct libdeflate_compressor *compressor,
			    const void *in, size_t in_nbytes,
			    void *out, size_t out_nbytes_avail);

/*
 * libdeflate_deflate_compress_bound() returns a worst-case upper bound on the
 * number of bytes of compressed data that may be produced by compressing any
 * buffer of length less than or equal to 'in_nbytes' using
 * libdeflate_deflate_compress() with the specified compressor.  Mathematically,
 * this bound will necessarily be a number greater than or equal to 'in_nbytes'.
 * It may be an overestimate of the true upper bound.  The return value is
 * guaranteed to be the same for all invocations with the same compressor and
 * same 'in_nbytes'.
 *
 * As a special case, 'compressor' may be NULL.  This causes the bound to be
 * taken across *any* libdeflate_compressor that could ever be allocated with
 * this build of the library, with any options.
 *
 * Note that this function is not necessary in many applications.  With
 * block-based compression, it is usually preferable to separately store the
 * uncompressed size of each block and to store any blocks that did not compress
 * to less than their original size uncompressed.  In that scenario, there is no
 * need to know the worst-case compressed size, since the maximum number of
 * bytes of compressed data that may be used would always be one less than the
 * input length.  You can just pass a buffer of that size to
 * libdeflate_deflate_compress() and store the data uncompressed if
 * libdeflate_deflate_compress() returns 0, indicating that the compressed data
 * did not fit into the provided output buffer.
 */
LIBDEFLATEAPI size_t
libdeflate_deflate_compress_bound(struct libdeflate_compressor *compressor,
				  size_t in_nbytes);

/*
 * Like libdeflate_deflate_compress(), but stores the data in the zlib wrapper
 * format.
 */
LIBDEFLATEAPI size_t
libdeflate_zlib_compress(struct libdeflate_compressor *compressor,
			 const void *in, size_t in_nbytes,
			 void *out, size_t out_nbytes_avail);

/*
 * Like libdeflate_deflate_compress_bound(), but assumes the data will be
 * compressed with libdeflate_zlib_compress() rather than with
 * libdeflate_deflate_compress().
 */
LIBDEFLATEAPI size_t
libdeflate_zlib_compress_bound(struct libdeflate_compressor *compressor,
			       size_t in_nbytes);

/*
 * Like libdeflate_deflate_compress(), but stores the data in the gzip wrapper
 * format.
 */
LIBDEFLATEAPI size_t
libdeflate_gzip_compress(struct libdeflate_compressor *compressor,
			 const void *in, size_t in_nbytes,
			 void *out, size_t out_nbytes_avail);

/*
 * Like libdeflate_deflate_compress_bound(), but assumes the data will be
 * compressed with libdeflate_gzip_compress() rather than with
 * libdeflate_deflate_compress().
 */
LIBDEFLATEAPI size_t
libdeflate_gzip_compress_bound(struct libdeflate_compressor *compressor,
			       size_t in_nbytes);

/*
 * libdeflate_free_compressor() frees a compressor that was allocated with
 * libdeflate_alloc_compressor().  If a NULL pointer is passed in, no action is
 * taken.
 */
LIBDEFLATEAPI void
libdeflate_free_compressor(struct libdeflate_compressor *compressor);

/* ========================================================================== */
/*                             Decompression                                  */
/* ========================================================================== */

struct libdeflate_decompressor;

/*
 * libdeflate_alloc_decompressor() allocates a new decompressor that can be used
 * for DEFLATE, zlib, and gzip decompression.  The return value is a pointer to
 * the new decompressor, or NULL if out of memory.
 *
 * This function takes no parameters, and the returned decompressor is valid for
 * decompressing data that was compressed at any compression level and with any
 * sliding window size.
 *
 * A single decompressor is not safe to use by multiple threads concurrently.
 * However, different threads may use different decompressors concurrently.
 */
LIBDEFLATEAPI struct libdeflate_decompressor *
libdeflate_alloc_decompressor(void);

/*
 * Result of a call to libdeflate_deflate_decompress(),
 * libdeflate_zlib_decompress(), or libdeflate_gzip_decompress().
 */
enum libdeflate_result {
	/* Decompression was successful.  */
	LIBDEFLATE_SUCCESS = 0,

	/* Decompressed failed because the compressed data was invalid, corrupt,
	 * or otherwise unsupported.  */
	LIBDEFLATE_BAD_DATA = 1,

	/* A NULL 'actual_out_nbytes_ret' was provided, but the data would have
	 * decompressed to fewer than 'out_nbytes_avail' bytes.  */
	LIBDEFLATE_SHORT_OUTPUT = 2,

	/* The data would have decompressed to more than 'out_nbytes_avail'
	 * bytes.  */
	LIBDEFLATE_INSUFFICIENT_SPACE = 3,
};

/*
 * libdeflate_deflate_decompress() decompresses 'in_nbytes' bytes of
 * raw DEFLATE-compressed data at 'in' and writes the uncompressed data to
 * 'out', which is a buffer of at least 'out_nbytes_avail' bytes.  If
 * decompression was successful, then 0 (LIBDEFLATE_SUCCESS) is returned;
 * otherwise, a nonzero result code such as LIBDEFLATE_BAD_DATA is returned.  If
 * a nonzero result code is returned, then the contents of the output buffer are
 * undefined.
 *
 * libdeflate_deflate_decompress() can be used in cases where the actual
 * uncompressed size is known (recommended) or unknown (not recommended):
 *
 *   - If the actual uncompressed size is known, then pass the actual
 *     uncompressed size as 'out_nbytes_avail' and pass NULL for
 *     'actual_out_nbytes_ret'.  This makes libdeflate_deflate_decompress() fail
 *     with LIBDEFLATE_SHORT_OUTPUT if the data decompressed to fewer than the
 *     specified number of bytes.
 *
 *   - If the actual uncompressed size is unknown, then provide a non-NULL
 *     'actual_out_nbytes_ret' and provide a buffer with some size
 *     'out_nbytes_avail' that you think is large enough to hold all the
 *     uncompressed data.  In this case, if the data decompresses to less than
 *     or equal to 'out_nbytes_avail' bytes, then
 *     libdeflate_deflate_decompress() will write the actual uncompressed size
 *     to *actual_out_nbytes_ret and return 0 (LIBDEFLATE_SUCCESS).  Otherwise,
 *     it will return LIBDEFLATE_INSUFFICIENT_SPACE if the provided buffer was
 *     not large enough but no other problems were encountered, or another
 *     nonzero result code if decompression failed for another reason.
 */
LIBDEFLATEAPI enum libdeflate_result
libdeflate_deflate_decompress(struct libdeflate_decompressor *decompressor,
			      const void *in, size_t in_nbytes,
			      void *out, size_t out_nbytes_avail,
			      size_t *actual_out_nbytes_ret);

/*
 * Like libdeflate_deflate_decompress(), but assumes the zlib wrapper format
 * instead of raw DEFLATE.
 */
LIBDEFLATEAPI enum libdeflate_result
libdeflate_zlib_decompress(struct libdeflate_decompressor *decompressor,
			   const void *in, size_t in_nbytes,
			   void *out, size_t out_nbytes_avail,
			   size_t *actual_out_nbytes_ret);

/*
 * Like libdeflate_deflate_decompress(), but assumes the gzip wrapper format
 * instead of raw DEFLATE.
 */
LIBDEFLATEAPI enum libdeflate_result
libdeflate_gzip_decompress(struct libdeflate_decompressor *decompressor,
			   const void *in, size_t in_nbytes,
			   void *out, size_t out_nbytes_avail,
			   size_t *actual_out_nbytes_ret);

/*
 * libdeflate_free_decompressor() frees a decompressor that was allocated with
 * libdeflate_alloc_decompressor().  If a NULL pointer is passed in, no action
 * is taken.
 */
LIBDEFLATEAPI void
libdeflate_free_decompressor(struct libdeflate_decompressor *decompressor);

/* ========================================================================== */
/*                                Checksums                                   */
/* ========================================================================== */

/*
 * libdeflate_adler32() updates a running Adler-32 checksum with 'len' bytes of
 * data and returns the updated checksum.  When starting a new checksum, the
 * required initial value for 'adler' is 1.  This value is also returned when
 * 'buffer' is specified as NULL.
 */
LIBDEFLATEAPI uint32_t
libdeflate_adler32(uint32_t adler32, const void *buffer, size_t len);


/*
 * libdeflate_crc32() updates a running CRC-32 checksum with 'len' bytes of data
 * and returns the updated checksum.  When starting a new checksum, the required
 * initial value for 'crc' is 0.  This value is also returned when 'buffer' is
 * specified as NULL.
 */
LIBDEFLATEAPI uint32_t
libdeflate_crc32(uint32_t crc, const void *buffer, size_t len);

#ifdef __cplusplus
}
#endif

#endif /* LIBDEFLATE_H */
