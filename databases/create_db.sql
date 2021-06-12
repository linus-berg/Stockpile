CREATE TABLE "packages" (
	"id"	TEXT NOT NULL,
	"version"	TEXT NOT NULL,
	"url"	TEXT NOT NULL,
	"processed"	INTEGER NOT NULL,
	PRIMARY KEY("version","id")
)

