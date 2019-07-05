﻿// <auto-generated />
using System;
using FlickrToCloud.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FlickrToCloud.Contracts.Migrations
{
    [DbContext(typeof(CloudCopyContext))]
    [Migration("20190705105410_AddSourceFileNameToFiles")]
    partial class AddSourceFileNameToFiles
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "2.2.3-servicing-35854");

            modelBuilder.Entity("FlickrToCloud.Contracts.Models.File", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("FileName");

                    b.Property<string>("MonitorUrl");

                    b.Property<string>("ResponseData");

                    b.Property<int>("SessionId");

                    b.Property<string>("SourceFileName");

                    b.Property<string>("SourceId");

                    b.Property<string>("SourcePath");

                    b.Property<string>("SourceUrl");

                    b.Property<int>("State");

                    b.HasKey("Id");

                    b.HasIndex("SessionId");

                    b.ToTable("Files");
                });

            modelBuilder.Entity("FlickrToCloud.Contracts.Models.Session", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("DestinationCloud");

                    b.Property<string>("DestinationFolder");

                    b.Property<int>("FilesOrigin");

                    b.Property<int>("Mode");

                    b.Property<string>("SourceCloud");

                    b.Property<DateTime>("Started");

                    b.Property<int>("State");

                    b.HasKey("Id");

                    b.ToTable("Sessions");
                });

            modelBuilder.Entity("FlickrToCloud.Contracts.Models.File", b =>
                {
                    b.HasOne("FlickrToCloud.Contracts.Models.Session", "Session")
                        .WithMany("Files")
                        .HasForeignKey("SessionId")
                        .OnDelete(DeleteBehavior.Cascade);
                });
#pragma warning restore 612, 618
        }
    }
}
