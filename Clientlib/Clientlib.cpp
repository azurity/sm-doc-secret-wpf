// Clientlib.cpp : 定义 DLL 应用程序的导出函数。
//

#include "stdafx.h"
#define _WINSOCK_DEPRECATED_NO_WARNINGS
#define _CRT_SECURE_NO_WARNINGS

#include <string>
#include <sstream>
#include <winsock2.h>
#include <openssl/ssl.h>
#include <openssl/err.h>
#include <openssl/evp.h>
#include <openssl/conf.h>
using namespace std;

#pragma comment( lib, "ws2_32.lib" )
#pragma comment(lib,"libssl.lib")
#pragma comment(lib,"libcrypto.lib")

#define DES_PORT 9000
#define PKTLEN 256

enum Command {
	CMD_UNKNOWN,
	CMD_REQ_ENC,
	CMD_REQ_DEC,
	CMD_SUCCESS,
	CMD_FAILED,
	CMD_END,
	CMD_HEART = 0x0f
};

struct ConnCTX {
	SOCKET socket;
	SSL *ssl;
	SSL_CTX *ctx;
};

string err = "";

void ShowCerts(SSL * ssl) {
	X509 *cert;
	char *line;
	cert = SSL_get_peer_certificate(ssl);
	if (cert != NULL)
	{
		printf("数字证书信息:\n");
		line = X509_NAME_oneline(X509_get_subject_name(cert), 0, 0);
		printf("证书: %s\n", line);
		OPENSSL_free(line);
		line = X509_NAME_oneline(X509_get_issuer_name(cert), 0, 0);
		printf("颁发者: %s\n", line);
		OPENSSL_free(line);
		X509_free(cert);
	}
	else
		printf("无证书信息！\n");
}

extern "C" __declspec(dllexport)
long CreateEnv() {
	WSADATA wsaData;
	WORD wVersionRequired;
	wVersionRequired = MAKEWORD(2, 2);
	if (WSAStartup(wVersionRequired, &wsaData) != 0)
	{
		return false;
	}
	return true;
}

extern "C" __declspec(dllexport)
void ReleaseEnv() {
	WSACleanup();
}

extern "C" __declspec(dllexport)
void* CreateConn(char * addr) {
	SOCKET sockfd;
	if ((sockfd = socket(AF_INET, SOCK_STREAM, 0)) < 0)
	{
		return nullptr;
	}
	ConnCTX *conn = new ConnCTX();
	memset(conn, 0, sizeof(ConnCTX));
	conn->socket = sockfd;
	SSL_library_init();
	OpenSSL_add_all_algorithms();
	SSL_load_error_strings();
	conn->ctx = SSL_CTX_new(TLSv1_client_method());
	if (conn->ctx == nullptr) {
		err = ERR_error_string(ERR_get_error(), nullptr);
		return conn;
	}
	sockaddr_in ser_addr;
	memset(&ser_addr, 0, sizeof(sockaddr_in));
	ser_addr.sin_addr.S_un.S_addr = inet_addr(addr);
	ser_addr.sin_port = htons(DES_PORT);
	ser_addr.sin_family = AF_INET;
	if (connect(sockfd, (SOCKADDR *)&ser_addr, sizeof(SOCKADDR_IN)) != 0) {
		stringstream ss;
		ss << WSAGetLastError();
		ss >> err;
		return conn;
	}
	conn->ssl = SSL_new(conn->ctx);
	SSL_set_fd(conn->ssl, conn->socket);
	if (SSL_connect(conn->ssl) == -1) {
		err = ERR_error_string(ERR_get_error(), nullptr);
		return conn;
	}
	return conn;
}

extern "C" __declspec(dllexport)
void CloseConn(void * conn) {
	if (conn == nullptr)return;
	ConnCTX * cconn = (ConnCTX*)conn;
	if (cconn->ssl != nullptr) {
		SSL_shutdown(cconn->ssl);
		SSL_free(cconn->ssl);
	}
	if (cconn->ctx != nullptr) {
		SSL_CTX_free(cconn->ctx);
	}
	closesocket(cconn->socket);
	delete cconn;
	return;
}

extern "C" __declspec(dllexport)
long SendPack(void * conn, uint8_t * buffer) {
	if (conn == nullptr)return false;
	ConnCTX*cconn = (ConnCTX*)conn;
	int ret = SSL_write(cconn->ssl, buffer, PKTLEN);
	return (ret > 0);
}

extern "C" __declspec(dllexport)
long RecvPack(void * conn, uint8_t * buffer) {
	if (conn == nullptr)return false;
	ConnCTX*cconn = (ConnCTX*)conn;
	int ret = SSL_read(cconn->ssl, buffer, PKTLEN);
	return (ret > 0);
}

extern "C" __declspec(dllexport)
const char* getError() {
	return err.c_str();
}

extern "C" __declspec(dllexport)
long fileHash(const char * filePath, uint8_t * hash) {
	FILE *f = nullptr;
	fopen_s(&f, filePath, "rb");
	if (f == nullptr) {
		return false;
	}
	EVP_MD_CTX *evpCtx = EVP_MD_CTX_new();
	if (evpCtx == nullptr) {
		fclose(f);
		err = ERR_error_string(ERR_get_error(), nullptr);
		return false;
	}
	EVP_DigestInit_ex(evpCtx, EVP_sha256(), NULL);
	uint8_t data[256];
	size_t len;
	while (true) {
		len = fread(data, 256, 1, f);
		EVP_DigestUpdate(evpCtx, data, len);
		if (len < 256) {
			break;
		}
	}
	fclose(f);
	unsigned rlen;
	EVP_DigestFinal_ex(evpCtx, hash, &rlen);
	return true;
}

extern "C" __declspec(dllexport)
long encfile(const char * inFilePath, const char * outFilePath, uint8_t * key, uint8_t * iv, uint8_t * hash) {
	FILE *in = nullptr;
	FILE *out = nullptr;
	fopen_s(&in, inFilePath, "rb");
	fopen_s(&out, outFilePath, "wb");
	if (in == nullptr || out == nullptr) {
		if (in)fclose(in);
		if (out)fclose(out);
		return false;
	}
	ERR_load_crypto_strings();
	OpenSSL_add_all_algorithms();
	OPENSSL_config(NULL);
	EVP_CIPHER_CTX *ctx = EVP_CIPHER_CTX_new();
	if (ctx == nullptr) {
		fclose(in);
		fclose(out);
		err = ERR_error_string(ERR_get_error(), nullptr);
		return false;
	}
	EVP_MD_CTX *evpCtx = EVP_MD_CTX_new();
	if (evpCtx == nullptr) {
		EVP_CIPHER_CTX_free(ctx);
		fclose(in);
		fclose(out);
		err = ERR_error_string(ERR_get_error(), nullptr);
		return false;
	}
	if (1 != EVP_EncryptInit_ex(ctx, EVP_sm4_cbc(), NULL, key, iv)
		|| 1 != EVP_DigestInit_ex(evpCtx, EVP_sha256(), NULL)) {
		EVP_CIPHER_CTX_free(ctx);
		EVP_MD_CTX_free(evpCtx);
		fclose(in);
		fclose(out);
		err = ERR_error_string(ERR_get_error(), nullptr);
		return false;
	}
	uint8_t dataIn[256];
	uint8_t dataOut[1024];
	int inLen, outLen;
	while (true) {
		inLen = fread(dataIn, 256, 1, in);
		EVP_EncryptUpdate(ctx, dataOut, &outLen, dataIn, inLen);
		fwrite(dataOut, outLen, 1, out);
		EVP_DigestUpdate(evpCtx, dataOut, outLen);
		if (inLen < 256) {
			break;
		}
	}
	EVP_EncryptFinal_ex(ctx, dataOut, &outLen);
	fwrite(dataOut, outLen, 1, out);
	fclose(in);
	fclose(out);
	unsigned rlen;
	EVP_DigestFinal_ex(evpCtx, hash, &rlen);
	EVP_MD_CTX_free(evpCtx);
	EVP_CIPHER_CTX_free(ctx);
	EVP_cleanup();
	CRYPTO_cleanup_all_ex_data();
	ERR_free_strings();
	return true;
}

extern "C" __declspec(dllexport)
long decfile(const char * inFilePath, const char * outFilePath, uint8_t * key, uint8_t * iv, uint8_t * hash) {
	FILE *in = nullptr;
	FILE *out = nullptr;
	fopen_s(&in, inFilePath, "rb");
	fopen_s(&out, outFilePath, "wb");
	if (in == nullptr || out == nullptr) {
		if (in)fclose(in);
		if (out)fclose(out);
		return false;
	}
	ERR_load_crypto_strings();
	OpenSSL_add_all_algorithms();
	OPENSSL_config(NULL);
	EVP_CIPHER_CTX *ctx = EVP_CIPHER_CTX_new();
	if (ctx == nullptr) {
		fclose(in);
		fclose(out);
		err = ERR_error_string(ERR_get_error(), nullptr);
		return false;
	}
	EVP_MD_CTX *evpCtx = EVP_MD_CTX_new();
	if (evpCtx == nullptr) {
		EVP_CIPHER_CTX_free(ctx);
		fclose(in);
		fclose(out);
		err = ERR_error_string(ERR_get_error(), nullptr);
		return false;
	}
	if (1 != EVP_DecryptInit_ex(ctx, EVP_sm4_cbc(), NULL, key, iv)
		|| 1 != EVP_DigestInit_ex(evpCtx, EVP_sha256(), NULL)) {
		EVP_CIPHER_CTX_free(ctx);
		EVP_MD_CTX_free(evpCtx);
		fclose(in);
		fclose(out);
		err = ERR_error_string(ERR_get_error(), nullptr);
		return false;
	}
	uint8_t dataIn[256];
	uint8_t dataOut[1024];
	int inLen, outLen;
	while (true) {
		inLen = fread(dataIn, 256, 1, in);
		EVP_DecryptUpdate(ctx, dataOut, &outLen, dataIn, inLen);
		fwrite(dataOut, outLen, 1, out);
		EVP_DigestUpdate(evpCtx, dataOut, outLen);
		if (inLen < 256) {
			break;
		}
	}
	EVP_DecryptFinal_ex(ctx, dataOut, &outLen);
	fwrite(dataOut, outLen, 1, out);
	fclose(in);
	fclose(out);
	unsigned rlen;
	EVP_DigestFinal_ex(evpCtx, hash, &rlen);
	EVP_MD_CTX_free(evpCtx);
	EVP_CIPHER_CTX_free(ctx);
	EVP_cleanup();
	CRYPTO_cleanup_all_ex_data();
	ERR_free_strings();
	return true;
}